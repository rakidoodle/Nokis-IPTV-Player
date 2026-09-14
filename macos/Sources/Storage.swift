import Foundation
import Security
import CryptoKit

struct SecureStore {
    static let service="com.noki.iptv"
    static func save(_ data:Data,account:String) throws {
        let query:[String:Any]=[kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:service,kSecAttrAccount as String:account]
        let update=SecItemUpdate(query as CFDictionary,[kSecValueData as String:data] as CFDictionary)
        if update == errSecItemNotFound {
            var entry=query; entry[kSecValueData as String]=data; entry[kSecAttrAccessible as String]=kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            guard SecItemAdd(entry as CFDictionary,nil) == errSecSuccess else { throw PlayerError.message("Could not save credentials to Keychain. Allow access and try again.") }
        } else if update != errSecSuccess { throw PlayerError.message("Keychain access failed. Allow access and try again.") }
    }
    static func load(_ account:String) throws -> Data? {
        let query:[String:Any]=[kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:service,kSecAttrAccount as String:account,kSecReturnData as String:true,kSecMatchLimit as String:kSecMatchLimitOne,kSecUseAuthenticationUI as String:kSecUseAuthenticationUIFail]
        var item:CFTypeRef?; let status=SecItemCopyMatching(query as CFDictionary,&item)
        if status == errSecItemNotFound { return nil }; guard status == errSecSuccess else { throw PlayerError.message("macOS denied access to these saved credentials. Unlock your login Keychain, or edit this source and re-enter its settings.") }; return item as? Data
    }
    static func delete(_ account:String) { SecItemDelete([kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:service,kSecAttrAccount as String:account] as CFDictionary) }
    static func credentials(_ sourceID:String) throws -> Credentials {
        guard let data=try load(sourceID) else { throw PlayerError.message("Source credentials are missing. Edit the source to enter them again.") }; return try JSONDecoder().decode(Credentials.self,from:data)
    }
}
struct LibrarySnapshot: Codable { var sources:[Source]=[]; var favorites:Set<String>=[] }
struct CachedSource: Codable { var catalog:Catalog; var programmes:[Programme]; var guideUpdated:Date? }
final class DiskStore {
    let directory:URL
    init(directory:URL?=nil) {
        self.directory=directory ?? FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("Noki IPTV")
        try? FileManager.default.createDirectory(at:self.directory,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700])
    }
    func load() throws -> LibrarySnapshot {
        let path=directory.appendingPathComponent("library.json"); if !FileManager.default.fileExists(atPath:path.path) { return LibrarySnapshot() }; return try JSONDecoder().decode(LibrarySnapshot.self,from:Data(contentsOf:path))
    }
    func save(_ snapshot:LibrarySnapshot) throws { try JSONEncoder().encode(snapshot).write(to:directory.appendingPathComponent("library.json"),options:.atomic) }
    func cacheKey() throws -> SymmetricKey {
        if let data=try SecureStore.load("cache-encryption-key") { return SymmetricKey(data:data) }
        let key=SymmetricKey(size:.bits256); try SecureStore.save(key.withUnsafeBytes { Data($0) },account:"cache-encryption-key"); return key
    }
    func cache(_ snapshot:CachedSource,sourceID:String) throws {
        let data=try JSONEncoder().encode(snapshot); let box=try AES.GCM.seal(data,using:cacheKey())
        try box.combined!.write(to:directory.appendingPathComponent(stableID(sourceID)+".cache"),options:.atomic)
    }
    func cached(_ sourceID:String) throws -> CachedSource? {
        let url=directory.appendingPathComponent(stableID(sourceID)+".cache"); guard FileManager.default.fileExists(atPath:url.path) else { return nil }
        let box=try AES.GCM.SealedBox(combined:Data(contentsOf:url)); return try JSONDecoder().decode(CachedSource.self,from:AES.GCM.open(box,using:cacheKey()))
    }
    func removeCache(_ sourceID:String) { try? FileManager.default.removeItem(at:directory.appendingPathComponent(stableID(sourceID)+".cache")) }
}
