import SwiftUI
import AppKit

@MainActor final class Library: ObservableObject {
    @Published var sources:[Source]=[]
    @Published var selectedSourceID:String? { didSet { if selectedSourceID != oldValue { switchSource() } } }
    @Published var catalog=Catalog()
    @Published var programmes:[Programme]=[] { didSet { guideIndex=Dictionary(grouping:programmes,by: \.channel) } }
    private var guideIndex:[String:[Programme]]=[:]
    @Published var favorites:Set<String>=[]
    @Published var connectionStatus="Choose a source to connect"
    @Published var loading=false
    @Published var guideLoading=false
    @Published var error:String?
    @Published var guideStatus="No guide loaded"
    @Published var section="Live TV"
    @Published var query=""
    @Published var group="All groups"
    @Published var episodes:[MediaItem]=[]
    @Published var series:MediaItem?
    @Published var episodeLoading=false
    @Published var guideUpdated:Date?
    var provider:Provider?
    private let disk=DiskStore()
    private var loadTask:Task<Void,Never>?
    private var guideTask:Task<Void,Never>?
    private var episodesTask:Task<Void,Never>?
    private var refreshTimer:Timer?
    var source:Source? { sources.first { $0.id == selectedSourceID } }
    init() {
        do { let stored=try disk.load(); sources=stored.sources; favorites=stored.favorites } catch { self.error="Saved library could not be read. Your existing files have been preserved." }
        if !CommandLine.arguments.contains("--smoke-test"), let first=sources.first { Task { await Task.yield(); selectedSourceID=first.id } }
        refreshTimer=Timer.scheduledTimer(withTimeInterval:60,repeats:true) { [weak self] _ in Task { @MainActor in
            guard let self,self.provider != nil,!self.guideLoading else { return }; if self.guideUpdated.map({ Date().timeIntervalSince($0) >= 21600 }) ?? false { self.refreshGuide() }
        } }
    }
    func persist() { do { try disk.save(LibrarySnapshot(sources:sources,favorites:favorites)) } catch { self.error="Changes could not be saved. Check available disk space." } }
    func toggleFavorite(_ item:MediaItem) { if favorites.contains(item.id) { favorites.remove(item.id) } else { favorites.insert(item.id) }; persist() }
    func saveSource(_ source:Source,credentials:Credentials) throws {
        _ = try validURL(credentials.endpoint)
        guard !source.name.trimmingCharacters(in:.whitespaces).isEmpty else { throw PlayerError.message("Give this source a name.") }
        if source.kind == .xtream && (credentials.username.isEmpty || credentials.password.isEmpty) { throw PlayerError.message("Enter the Xtream username and password.") }
        if source.kind == .stalker && credentials.mac.range(of:#"^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$"#,options:.regularExpression) == nil { throw PlayerError.message("Enter a registered MAC in the form 00:1A:79:12:34:56.") }
        if !credentials.epg.isEmpty { _ = try validURL(credentials.epg) }
        try SecureStore.save(JSONEncoder().encode(credentials),account:source.id)
        if let index=sources.firstIndex(where: { $0.id == source.id }) { sources[index]=source } else { sources.append(source) }
        persist(); disk.removeCache(source.id)
        if selectedSourceID == source.id { switchSource() } else { selectedSourceID=source.id }
    }
    func removeSource(_ source:Source) {
        sources.removeAll { $0.id == source.id }; favorites=favorites.filter { !$0.hasPrefix(source.id+":") }; SecureStore.delete(source.id); disk.removeCache(source.id); persist()
        if selectedSourceID == source.id { selectedSourceID=sources.first?.id }
    }
    func switchSource() {
        loadTask?.cancel(); guideTask?.cancel(); episodesTask?.cancel()
        catalog=Catalog(); programmes=[]; episodes=[]; series=nil; provider=nil; group="All groups"; guideUpdated=nil; guideStatus="No guide loaded"; loading=false; guideLoading=false; episodeLoading=false
        guard let source else { return }
        loading=true; connectionStatus="Opening saved source…"
        loadTask=Task {
            do {
                let store=disk
                let (credentials,cached)=try await Task.detached { (try SecureStore.credentials(source.id), try? store.cached(source.id)) }.value
                try Task.checkCancellation(); guard selectedSourceID == source.id else { return }
                provider=makeProvider(source,credentials)
                if let cached { catalog=cached.catalog; programmes=cached.programmes; guideUpdated=cached.guideUpdated; guideStatus=programmes.isEmpty ? "No guide loaded" : "Cached guide" }
                loading=false; loadTask=nil; refresh()
            } catch { guard !Task.isCancelled else { return }; loading=false; connectionStatus="Could not open source credentials"; self.error=(error as? PlayerError)?.localizedDescription ?? "The saved source could not be opened. Edit its settings and retry." }
        }
    }
    func refresh() {
        guard let source else { return }; guard let provider else { switchSource(); return }; loadTask?.cancel(); loading=true; connectionStatus="Connecting to \(source.kind.rawValue)…"
        loadTask=Task {
            do {
                let result=try await provider.catalog(); try Task.checkCancellation(); guard selectedSourceID == source.id else { return }
                var merged=result; merged.items=uniqueItems(result.items + catalog.items.filter { $0.kind == .episode && favorites.contains($0.id) }); catalog=merged; loading=false; connectionStatus="\(catalog.items.count.formatted()) titles loaded"; saveCache(); refreshGuide()
            } catch { guard !Task.isCancelled,selectedSourceID == source.id else { return }; loading=false; connectionStatus="Connection failed — edit source or retry"; self.error=(error as? PlayerError)?.localizedDescription ?? "The catalog could not be loaded. Check source settings and retry." }
        }
    }
    func saveCache() { guard let id=selectedSourceID else { return }; do { try disk.cache(CachedSource(catalog:catalog,programmes:programmes,guideUpdated:guideUpdated),sourceID:id) } catch { self.error="The library cache could not be saved. Check Keychain access and disk space." } }
    func refreshGuide() {
        guard let source,let provider else { return }; guideTask?.cancel(); guideLoading=true; guideStatus="Fetching guide…"
        let items=catalog.items; let metadata=catalog.epgURL
        guideTask=Task {
            do {
                let credentials=try SecureStore.credentials(source.id); let url=credentials.epg.isEmpty ? metadata : credentials.epg
                let result=try await provider.guide(for:items,explicitURL:url); try Task.checkCancellation(); guard selectedSourceID == source.id else { return }
                programmes=result; guideUpdated=Date(); guideLoading=false; guideStatus=result.isEmpty ? "No guide available — add an XMLTV URL in source settings" : "Updated \(Date().formatted(date:.omitted,time:.shortened))"; saveCache()
            } catch { guard !Task.isCancelled,selectedSourceID == source.id else { return }; guideLoading=false; guideStatus="Guide refresh failed. Check the EPG URL and retry." }
        }
    }
    func openSeries(_ item:MediaItem) {
        series=item; episodes=[]; episodeLoading=true; episodesTask?.cancel(); guard let provider else { return }
        episodesTask=Task { do { let result=try await provider.episodes(for:item); try Task.checkCancellation(); guard series?.id == item.id else { return }; episodes=result; catalog.items=uniqueItems(catalog.items + result); saveCache(); episodeLoading=false }
            catch { guard !Task.isCancelled else { return }; episodeLoading=false; self.error=(error as? PlayerError)?.localizedDescription ?? "Episodes could not be loaded. Try again." } }
    }
    func guide(_ item:MediaItem) -> [Programme] { (guideIndex[item.epgID.isEmpty ? item.providerID : item.epgID] ?? []).filter { $0.end > Date() }.sorted { $0.start < $1.start } }
    var filtered:[MediaItem] { catalog.items.filter { item in
        (section == "Favorites" ? favorites.contains(item.id) : item.kind.rawValue == section) && (group == "All groups" || group == item.group) && (query.isEmpty || item.name.localizedCaseInsensitiveContains(query))
    } }
    var groups:[String] { ["All groups"] + Set(catalog.items.filter { section == "Favorites" ? favorites.contains($0.id) : $0.kind.rawValue == section }.map(\.group)).sorted() }
}
