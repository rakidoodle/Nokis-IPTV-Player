import Foundation
import CryptoKit

enum SourceKind: String, Codable, CaseIterable, Identifiable { case m3u = "M3U Playlist", xtream = "Xtream Codes", stalker = "Stalker Portal"; var id: String { rawValue } }
struct Source: Codable, Identifiable, Hashable { var id = UUID().uuidString; var name: String; var kind: SourceKind }
struct Credentials: Codable { var endpoint = ""; var username = ""; var password = ""; var mac = ""; var serial = ""; var deviceID = ""; var deviceID2 = ""; var epg = "" }
enum MediaKind: String, Codable, CaseIterable { case live = "Live TV", movie = "Movies", series = "Series", episode = "Episodes" }
struct MediaItem: Codable, Identifiable, Hashable {
    var id: String; var sourceID: String; var providerID: String; var name: String; var kind: MediaKind
    var group = "Uncategorized"; var logo = ""; var epgID = ""; var location = ""; var headers: [String:String] = [:]
    var season: Int?; var episode: Int?; var summary = ""; var subtitles: [String:String] = [:]
}
struct Programme: Codable, Identifiable, Hashable {
    var channel: String; var title: String; var start: Date; var end: Date; var detail = ""
    var id: String { "\(channel)|\(start.timeIntervalSince1970)|\(title)" }
}
struct Catalog: Codable { var items: [MediaItem] = []; var epgURL = ""; var warnings: [String] = [] }
struct Stream { var url: URL; var headers: [String:String] = [:] }
enum PlayerError: LocalizedError {
    case message(String)
    var errorDescription: String? { if case .message(let s) = self { return s }; return nil }
}
func stableID(_ text: String) -> String { SHA256.hash(data: Data(text.utf8)).map { String(format:"%02x", $0) }.joined() }
func string(_ value: Any?) -> String { if let s = value as? String { return s }; if let n = value as? NSNumber { return n.stringValue }; return "" }
func integer(_ value: Any?) -> Int { Int(string(value)) ?? 0 }
func validURL(_ text: String) throws -> URL {
    guard let url = URL(string: text), ["https", "http", "file"].contains(url.scheme?.lowercased() ?? ""), url.isFileURL || url.host != nil else { throw PlayerError.message("Enter a valid HTTP(S) URL or choose a local file.") }; return url
}
func decodedText(_ text: String) -> String { guard let d = Data(base64Encoded:text), let s = String(data:d,encoding:.utf8), !s.isEmpty else { return text }; return s }
func uniqueItems(_ items: [MediaItem]) -> [MediaItem] { var seen = Set<String>(); return items.filter { seen.insert($0.id).inserted } }

struct M3UParser {
    static func attributes(_ line: String) -> [String:String] {
        let pattern = #"([\w-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')"#
        let regex = try! NSRegularExpression(pattern: pattern)
        let ns = line as NSString
        return regex.matches(in:line,range:NSRange(location:0,length:ns.length)).reduce(into:[:]) { out, match in
            let value = match.range(at:2).location != NSNotFound ? match.range(at:2) : match.range(at:3)
            out[ns.substring(with:match.range(at:1)).lowercased()] = ns.substring(with:value)
        }
    }
    static func parse(_ text: String, sourceID: String, base: URL? = nil) throws -> Catalog {
        guard text.trimmingCharacters(in:.whitespacesAndNewlines).replacingOccurrences(of:"\u{feff}",with:"").hasPrefix("#EXTM3U") else { throw PlayerError.message("This file is not an M3U playlist. It must begin with #EXTM3U.") }
        // A media/master HLS playlist is itself a playable source, not a channel catalog.
        if text.contains("#EXT-X-") {
            guard let base else { throw PlayerError.message("This HLS playlist needs its original URL.") }
            return Catalog(items:[MediaItem(id:sourceID+":hls",sourceID:sourceID,providerID:"hls",name:base.deletingPathExtension().lastPathComponent,kind:.live,location:base.absoluteString)])
        }
        var result = Catalog(); var attrs: [String:String] = [:]; var title = ""; var group = ""; var headers: [String:String] = [:]
        for raw in text.components(separatedBy:.newlines) {
            let line = raw.trimmingCharacters(in:.whitespacesAndNewlines)
            if line.contains("#EXTM3U") { let a = attributes(line); result.epgURL = a["x-tvg-url"] ?? a["url-tvg"] ?? "" }
            else if line.hasPrefix("#EXTINF:") {
                attrs = attributes(line); headers = [:]; group = attrs["group-title"] ?? "Uncategorized"
                var quoted: Character?; var comma: String.Index?
                for i in line.indices { let c=line[i]; if c == "\"" || c == "'" { if quoted == c { quoted=nil } else if quoted == nil { quoted=c } }; if c == "," && quoted == nil { comma=i; break } }
                title = comma.map { String(line[line.index(after:$0)...]) } ?? attrs["tvg-name"] ?? "Channel"
            } else if line.hasPrefix("#EXTGRP:") { group=String(line.dropFirst(8)) }
            else if line.hasPrefix("#EXTVLCOPT:") {
                let pair=line.dropFirst(11).split(separator:"=",maxSplits:1).map(String.init)
                if pair.count == 2 { if pair[0] == "http-user-agent" { headers["User-Agent"]=pair[1] }; if pair[0] == "http-referrer" { headers["Referer"]=pair[1] } }
            } else if !line.isEmpty && !line.hasPrefix("#") {
                let pieces=line.split(separator:"|",maxSplits:1).map(String.init)
                guard let url=URL(string:pieces[0],relativeTo:base)?.absoluteURL, ["http","https","file","rtsp","rtmp","udp","rtp"].contains(url.scheme ?? "") else { continue }
                if pieces.count > 1 { for pair in pieces[1].split(separator:"&") { let kv=pair.split(separator:"=",maxSplits:1).map(String.init); if kv.count == 2 { let key=kv[0].lowercased(); if key == "user-agent" { headers["User-Agent"]=kv[1].removingPercentEncoding ?? kv[1] }; if key == "referer" { headers["Referer"]=kv[1].removingPercentEncoding ?? kv[1] } } } }
                let kind: MediaKind = ["mp4","mkv","avi","mov","webm"].contains(url.pathExtension.lowercased()) ? .movie : .live
                let identity=(attrs["tvg-id"].flatMap { $0.isEmpty ? nil : $0 }) ?? url.absoluteString
                result.items.append(MediaItem(id:sourceID+":"+stableID(identity+"|"+title),sourceID:sourceID,providerID:identity,name:title.isEmpty ? url.lastPathComponent : title,kind:kind,group:group.isEmpty ? "Uncategorized" : group,logo:attrs["tvg-logo"] ?? "",epgID:attrs["tvg-id"] ?? "",location:url.absoluteString,headers:headers))
                title=""; attrs=[:]; headers=[:]
            }
        }
        result.items=uniqueItems(result.items)
        guard !result.items.isEmpty else { throw PlayerError.message("No playable entries were found in this playlist.") }; return result
    }
}

final class XMLTVParser: NSObject, XMLParserDelegate {
    var programmes: [Programme] = []; private var current: Programme?; private var element=""; private var buffer=""
    static func date(_ text: String) -> Date? {
        let f=DateFormatter(); f.locale=Locale(identifier:"en_US_POSIX"); f.timeZone=TimeZone(secondsFromGMT:0)
        for format in ["yyyyMMddHHmmss Z","yyyyMMddHHmm Z","yyyyMMddHHmmss"] { f.dateFormat=format; if let d=f.date(from:text) { return d } }; return nil
    }
    static func parse(_ data: Data) throws -> [Programme] {
        let delegate=XMLTVParser(); let parser=XMLParser(data:data); parser.shouldResolveExternalEntities=false; parser.delegate=delegate
        guard parser.parse() else { throw PlayerError.message("The EPG XML could not be read. Check the guide URL.") }; return delegate.programmes.sorted { $0.start < $1.start }
    }
    func parser(_ parser: XMLParser, didStartElement elementName:String, namespaceURI:String?, qualifiedName:String?, attributes:[String:String]) {
        element=elementName; buffer=""
        if elementName == "programme", let start=Self.date(attributes["start"] ?? ""), let end=Self.date(attributes["stop"] ?? ""), end > start { current=Programme(channel:attributes["channel"] ?? "",title:"",start:start,end:end) }
    }
    func parser(_ parser: XMLParser, foundCharacters string:String) { buffer += string }
    func parser(_ parser: XMLParser, didEndElement elementName:String, namespaceURI:String?, qualifiedName:String?) {
        if elementName == "title", current?.title.isEmpty == true { current?.title=buffer.trimmingCharacters(in:.whitespacesAndNewlines) }
        if elementName == "desc" { current?.detail=buffer.trimmingCharacters(in:.whitespacesAndNewlines) }
        if elementName == "programme" { if let p=current { programmes.append(p) }; current=nil }; buffer=""
    }
}

protocol HTTPTransport { func data(for request:URLRequest) async throws -> Data }
struct NetworkTransport: HTTPTransport {
    func data(for request: URLRequest) async throws -> Data {
        do {
            let (data,response)=try await URLSession.shared.data(for:request)
            guard let r=response as? HTTPURLResponse else { throw PlayerError.message("The server returned an invalid response.") }
            if r.statusCode == 401 || r.statusCode == 403 { throw PlayerError.message("Authentication was rejected. Check your credentials and provider access.") }
            guard (200..<300).contains(r.statusCode) else { throw PlayerError.message("The server returned HTTP \(r.statusCode). Try again or check the source settings.") }
            return data
        } catch let e as PlayerError { throw e } catch is CancellationError { throw CancellationError() } catch { throw PlayerError.message("Unable to reach the source. Check the address and your connection, then retry.") }
    }
}
protocol Provider {
    func catalog() async throws -> Catalog
    func episodes(for item:MediaItem) async throws -> [MediaItem]
    func resolve(_ item:MediaItem) async throws -> Stream
    func guide(for items:[MediaItem], explicitURL:String) async throws -> [Programme]
}
func fetchData(_ url:URL, transport:HTTPTransport) async throws -> Data {
    if url.isFileURL { return try Data(contentsOf:url) }
    var r=URLRequest(url:url); r.timeoutInterval=30; return try await transport.data(for:r)
}
func guideData(_ url:URL, transport:HTTPTransport) async throws -> [Programme] {
    var data=try await fetchData(url,transport:transport)
    if data.starts(with:[0x1f,0x8b]) {
        let folder=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true); defer { try? FileManager.default.removeItem(at:folder) }
        let input=folder.appendingPathComponent("guide.gz"); let output=folder.appendingPathComponent("guide.xml")
        try data.write(to:input); FileManager.default.createFile(atPath:output.path,contents:nil)
        let handle=try FileHandle(forWritingTo:output); defer { try? handle.close() }
        let process=Process(); process.executableURL=URL(fileURLWithPath:"/usr/bin/gunzip"); process.arguments=["-c",input.path]; process.standardOutput=handle; process.standardError=FileHandle.nullDevice
        try process.run(); process.waitUntilExit(); guard process.terminationStatus == 0 else { throw PlayerError.message("The compressed EPG is damaged.") }; data=try Data(contentsOf:output)
    }
    return try XMLTVParser.parse(data)
}
struct M3UProvider: Provider {
    let source:Source; let credentials:Credentials; var transport:HTTPTransport=NetworkTransport()
    func catalog() async throws -> Catalog { let url=try validURL(credentials.endpoint); let data=try await fetchData(url,transport:transport); guard let text=String(data:data,encoding:.utf8) ?? String(data:data,encoding:.isoLatin1) else { throw PlayerError.message("The playlist text encoding is unsupported.") }; return try M3UParser.parse(text,sourceID:source.id,base:url) }
    func episodes(for item:MediaItem) async throws -> [MediaItem] { [] }
    func resolve(_ item:MediaItem) async throws -> Stream { guard let url=URL(string:item.location) else { throw PlayerError.message("The stream URL is invalid.") }; if url.isFileURL && !FileManager.default.isReadableFile(atPath:url.path) { throw PlayerError.message("The media file is missing or cannot be read. Choose the file again.") }; return Stream(url:url,headers:item.headers) }
    func guide(for items:[MediaItem], explicitURL:String) async throws -> [Programme] { if explicitURL.isEmpty { return [] }; return try await guideData(validURL(explicitURL),transport:transport) }
}

func subtitleURLs(_ value:Any?) -> [String:String] {
    if let map=value as? [String:String] { return map.filter { URL(string:$0.value)?.scheme?.hasPrefix("http") == true } }
    var result:[String:String]=[:]
    for row in value as? [[String:Any]] ?? [] {
        let url=string(row["url"] ?? row["src"] ?? row["file"])
        if URL(string:url)?.scheme?.hasPrefix("http") == true { let name=string(row["language"] ?? row["label"]); result[name.isEmpty ? "Subtitle \(result.count+1)" : name]=url }
    }; return result
}
