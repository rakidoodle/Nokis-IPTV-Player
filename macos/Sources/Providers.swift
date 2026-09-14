import Foundation

final class XtreamProvider: Provider {
    let source:Source; let credentials:Credentials; let transport:HTTPTransport
    init(source:Source, credentials:Credentials, transport:HTTPTransport=NetworkTransport()) { self.source=source; self.credentials=credentials; self.transport=transport }
    func endpoint(_ file:String, params:[String:String]=[:]) throws -> URL {
        var base=try validURL(credentials.endpoint)
        if base.pathExtension == "php" { base.deleteLastPathComponent() }
        var c=URLComponents(url:base.appendingPathComponent(file),resolvingAgainstBaseURL:false)!
        c.queryItems=(["username":credentials.username,"password":credentials.password].merging(params) { _,new in new }).sorted { $0.key < $1.key }.map { URLQueryItem(name:$0.key,value:$0.value) }
        return c.url!
    }
    func api(_ action:String="", _ params:[String:String]=[:]) async throws -> Any {
        var p=params; if !action.isEmpty { p["action"]=action }
        let data=try await fetchData(endpoint("player_api.php",params:p),transport:transport)
        guard let json=try? JSONSerialization.jsonObject(with:data) else { throw PlayerError.message("The Xtream server returned invalid data.") }; return json
    }
    func catalog() async throws -> Catalog {
        let auth=try await api() as? [String:Any]; let info=auth?["user_info"] as? [String:Any]
        guard integer(info?["auth"]) == 1, string(info?["status"]).lowercased() != "expired", string(info?["status"]).lowercased() != "disabled" else { throw PlayerError.message("Xtream login failed or the subscription is inactive.") }
        var result=Catalog(epgURL:try endpoint("xmltv.php").absoluteString)
        for (kind,action,categoryAction) in [(MediaKind.live,"get_live_streams","get_live_categories"),(.movie,"get_vod_streams","get_vod_categories"),(.series,"get_series","get_series_categories")] {
            do {
                let categories=(try? await api(categoryAction)) as? [[String:Any]] ?? []
                let names=categories.reduce(into:[String:String]()) { $0[string($1["category_id"])]=string($1["category_name"]) }
                guard let rows=try await api(action) as? [[String:Any]] else { throw PlayerError.message("Invalid catalog.") }
                for row in rows {
                    let id=string(row[kind == .series ? "series_id" : "stream_id"]); guard !id.isEmpty else { continue }
                    let ext=string(row["container_extension"]); let folder=kind == .live ? "live" : "movie"
                    let url=kind == .series ? "" : try streamURL(folder:folder,id:id,ext:ext.isEmpty ? (kind == .live ? "ts" : "mp4") : ext).absoluteString
                    result.items.append(MediaItem(id:"\(source.id):\(kind.rawValue):\(id)",sourceID:source.id,providerID:id,name:string(row["name"]),kind:kind,group:names[string(row["category_id"])] ?? "Uncategorized",logo:string(row["stream_icon"] ?? row["cover"]),epgID:string(row["epg_channel_id"]),location:url,summary:string(row["plot"]),subtitles:subtitleURLs(row["subtitles"])))
                }
            } catch { result.warnings.append("\(kind.rawValue) could not be loaded. Refresh to retry.") }
        }
        if result.items.isEmpty && !result.warnings.isEmpty { throw PlayerError.message(result.warnings.joined(separator:" ")) }
        result.items=uniqueItems(result.items); return result
    }
    func streamURL(folder:String,id:String,ext:String) throws -> URL {
        var base=try validURL(credentials.endpoint); if base.pathExtension == "php" { base.deleteLastPathComponent() }
        for part in [folder,credentials.username,credentials.password,id+"."+ext] { base.appendPathComponent(part) }; return base
    }
    func episodes(for item:MediaItem) async throws -> [MediaItem] {
        let json=try await api("get_series_info",["series_id":item.providerID]) as? [String:Any]
        let seasons=json?["episodes"] as? [String:Any] ?? [:]; var items:[MediaItem]=[]
        for (season,raw) in seasons { for row in raw as? [[String:Any]] ?? [] {
            let id=string(row["id"]); guard !id.isEmpty else { continue }; let ext=string(row["container_extension"])
            let info=row["info"] as? [String:Any] ?? [:]
            items.append(MediaItem(id:"\(source.id):episode:\(id)",sourceID:source.id,providerID:id,name:string(row["title"]),kind:.episode,group:item.name,logo:item.logo,location:try streamURL(folder:"series",id:id,ext:ext.isEmpty ? "mp4" : ext).absoluteString,season:Int(season) ?? integer(row["season"]),episode:integer(row["episode_num"]),summary:string(info["plot"]),subtitles:subtitleURLs(info["subtitles"] ?? row["subtitles"])))
        } }
        return uniqueItems(items).sorted { ($0.season ?? 0,$0.episode ?? 0) < ($1.season ?? 0,$1.episode ?? 0) }
    }
    func resolve(_ item:MediaItem) async throws -> Stream { guard let url=URL(string:item.location) else { throw PlayerError.message("No playable stream was supplied.") }; return Stream(url:url,headers:item.headers) }
    func guide(for items:[MediaItem], explicitURL:String) async throws -> [Programme] { try await guideData(explicitURL.isEmpty ? endpoint("xmltv.php") : validURL(explicitURL),transport:transport) }
}

actor StalkerProvider: Provider {
    let source:Source; let credentials:Credentials; let transport:HTTPTransport
    var token=""
    init(source:Source,credentials:Credentials,transport:HTTPTransport=NetworkTransport()) { self.source=source; self.credentials=credentials; self.transport=transport }
    func portal() throws -> URL {
        var url=try validURL(credentials.endpoint)
        if url.lastPathComponent == "c" || url.lastPathComponent == "index.html" { url.deleteLastPathComponent(); if url.lastPathComponent == "c" { url.deleteLastPathComponent() } }
        if url.pathExtension != "php" { url.appendPathComponent("server/load.php") }; return url
    }
    func request(_ type:String,_ action:String,_ extra:[String:String]=[:],renew:Bool=true) async throws -> Any {
        if token.isEmpty && action != "handshake" { try await handshake() }
        var c=URLComponents(url:try portal(),resolvingAgainstBaseURL:false)!
        c.queryItems=(["type":type,"action":action,"JsHttpRequest":"1-xml"].merging(extra) { _,new in new }).sorted { $0.key < $1.key }.map { URLQueryItem(name:$0.key,value:$0.value) }
        var r=URLRequest(url:c.url!); r.timeoutInterval=30
        r.setValue("mac=\(credentials.mac.addingPercentEncoding(withAllowedCharacters:.alphanumerics) ?? credentials.mac); stb_lang=en; timezone=\(TimeZone.current.identifier)",forHTTPHeaderField:"Cookie")
        r.setValue("Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 MAG254 stbapp",forHTTPHeaderField:"User-Agent")
        r.setValue("Model: MAG254; Link: Ethernet",forHTTPHeaderField:"X-User-Agent")
        r.setValue(credentials.endpoint,forHTTPHeaderField:"Referer")
        if !token.isEmpty { r.setValue("Bearer \(token)",forHTTPHeaderField:"Authorization") }
        do {
            let data=try await transport.data(for:r)
            guard let root=try? JSONSerialization.jsonObject(with:data) as? [String:Any], let js=root["js"], !(js is NSNull) else { throw PlayerError.message("The Stalker portal returned invalid data or rejected this device.") }
            if let obj=js as? [String:Any], !string(obj["error"]).isEmpty { throw PlayerError.message("The Stalker portal rejected the request. Check device registration.") }
            return js
        } catch {
            if renew && action != "handshake" { token=""; try await handshake(); return try await request(type,action,extra,renew:false) }; throw error
        }
    }
    func handshake() async throws {
        let js=try await request("stb","handshake",["token":""],renew:false) as? [String:Any]
        token=string(js?["token"]); guard !token.isEmpty else { throw PlayerError.message("Stalker handshake failed. Check the portal URL and registered MAC address.") }
    }
    func rows(_ value:Any) -> [[String:Any]] { if let a=value as? [[String:Any]] { return a }; return (value as? [String:Any])?["data"] as? [[String:Any]] ?? [] }
    func paged(_ type:String,params:[String:String]=[:]) async throws -> [[String:Any]] {
        var result:[[String:Any]]=[]; var seen=Set<String>(); var page=1
        while page <= 10000 {
            try Task.checkCancellation()
            let raw=try await request(type,"get_ordered_list",params.merging(["p":String(page),"sortby":"name","not_ended":"0"]) { _,new in new })
            let batch=rows(raw); if batch.isEmpty { break }
            let fingerprint=stableID(batch.map { string($0["id"])+string($0["name"])+string($0["cmd"]) }.joined(separator:"|"))
            guard seen.insert(fingerprint).inserted else { throw PlayerError.message("The portal repeated a catalog page. Refresh or check provider compatibility.") }
            result += batch
            let object=raw as? [String:Any] ?? [:]; let total=integer(object["total_items"]); let perPage=integer(object["max_page_items"])
            if (total > 0 && result.count >= total) || (perPage > 0 && batch.count < perPage) || (total == 0 && perPage == 0) { break }; page += 1
        }
        return result
    }
    func catalog() async throws -> Catalog {
        try await handshake()
        var profile=["hd":"1","stb_type":"MAG254","num_banks":"2","sn":credentials.serial,"device_id":credentials.deviceID,"device_id2":credentials.deviceID2]
        if !credentials.username.isEmpty { profile["login"]=credentials.username; profile["password"]=credentials.password }
        if !credentials.username.isEmpty {
            let auth=try await request("stb","do_auth",["login":credentials.username,"password":credentials.password])
            if let ok=auth as? Bool, !ok { throw PlayerError.message("Stalker username/password authentication failed.") }
        }
        let info=try await request("stb","get_profile",profile) as? [String:Any] ?? [:]
        if integer(info["blocked"]) == 1 || string(info["status"]) == "blocked" { throw PlayerError.message("This Stalker device is blocked. Contact your provider.") }
        var result=Catalog()
        for (kind,type) in [(MediaKind.live,"itv"),(.movie,"vod"),(.series,"series")] {
            do {
                let categories=rows((try? await request(type,kind == .live ? "get_genres" : "get_categories")) ?? [])
                let names=categories.reduce(into:[String:String]()) { $0[string($1["id"])]=string($1["title"] ?? $1["name"]) }
                let records:[[String:Any]]
                if kind == .live { records=rows(try await request(type,"get_all_channels")) } else { records=try await paged(type) }
                for row in records {
                    let id=string(row["id"]); guard !id.isEmpty else { continue }
                    result.items.append(MediaItem(id:"\(source.id):\(kind.rawValue):\(id)",sourceID:source.id,providerID:id,name:string(row["name"]),kind:kind,group:names[string(row["tv_genre_id"] ?? row["category_id"])] ?? "Uncategorized",logo:string(row["logo"] ?? row["screenshot_uri"]),epgID:string(row["xmltv_id"]).isEmpty ? id : string(row["xmltv_id"]),location:string(row["cmd"]),summary:string(row["description"])))
                }
            } catch { result.warnings.append("\(kind.rawValue) could not be loaded from this portal.") }
        }
        if result.items.isEmpty && !result.warnings.isEmpty { throw PlayerError.message(result.warnings.joined(separator:" ")) }; result.items=uniqueItems(result.items); return result
    }
    func episodes(for item:MediaItem) async throws -> [MediaItem] {
        let seasons=try await paged("series",params:["movie_id":item.providerID])
        var result:[MediaItem]=[]
        for season in seasons {
            let seasonID=string(season["id"]); let number=integer(season["season_number"] ?? season["number"])
            let rawEpisodes=season["series"] as? [Any]
            if let rawEpisodes {
                for raw in rawEpisodes {
                    let row=raw as? [String:Any]; let ep=integer(row?["series_number"] ?? row?["id"] ?? raw)
                    var item=MediaItem(id:"\(source.id):episode:\(seasonID):\(ep)",sourceID:source.id,providerID:seasonID,name:row.map { string($0["name"]) } ?? "Episode \(ep)",kind:.episode,group:item.name,logo:item.logo,location:string(season["cmd"]),season:number,episode:ep)
                    if item.name.isEmpty { item.name="Episode \(ep)" }; result.append(item)
                }
            } else {
                let episodes=try await paged("series",params:["movie_id":item.providerID,"season_id":seasonID])
                for row in episodes { let id=string(row["id"]); result.append(MediaItem(id:"\(source.id):episode:\(id)",sourceID:source.id,providerID:id,name:string(row["name"]),kind:.episode,group:item.name,logo:item.logo,location:string(row["cmd"]),season:number,episode:integer(row["series_number"] ?? row["number"]))) }
            }
        }
        return uniqueItems(result).sorted { ($0.season ?? 0,$0.episode ?? 0) < ($1.season ?? 0,$1.episode ?? 0) }
    }
    func resolve(_ item:MediaItem) async throws -> Stream {
        let type=item.kind == .live ? "itv" : "vod"
        var p=["cmd":item.location,"forced_storage":"0","disable_ad":"0","download":"0"]
        if let episode=item.episode { p["series"]=String(episode) }
        let js=try await request(type,"create_link",p) as? [String:Any] ?? [:]
        let command=string(js["cmd"])
        guard let range=command.range(of:#"(?:https?|rtsp|rtmp)://[^\s]+"#,options:.regularExpression), let url=URL(string:String(command[range])) else { throw PlayerError.message("The portal did not return a playable link. Check subscription and device settings.") }
        return Stream(url:url,headers:["User-Agent":"Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 MAG254 stbapp","Referer":credentials.endpoint])
    }
    func guide(for items:[MediaItem], explicitURL:String) async throws -> [Programme] {
        if !explicitURL.isEmpty { return try await guideData(validURL(explicitURL),transport:transport) }
        let raw=try await request("itv","get_epg_info",["period":"24"]) as? [String:Any] ?? [:]
        let data=raw["data"] as? [String:Any] ?? raw; var result:[Programme]=[]
        let map=items.reduce(into:[String:String]()) { $0[$1.providerID]=$1.epgID }
        for (channel,value) in data { for row in value as? [[String:Any]] ?? [] {
            let start=Double(string(row["start_timestamp"])) ?? 0; let end=Double(string(row["stop_timestamp"] ?? row["end_timestamp"])) ?? 0
            if end > start && start > 0 { result.append(Programme(channel:map[channel] ?? channel,title:string(row["name"] ?? row["title"]),start:Date(timeIntervalSince1970:start),end:Date(timeIntervalSince1970:end),detail:string(row["descr"]))) }
        } }; return result.sorted { $0.start < $1.start }
    }
}
func makeProvider(_ source:Source,_ credentials:Credentials) -> Provider {
    switch source.kind { case .m3u: return M3UProvider(source:source,credentials:credentials); case .xtream: return XtreamProvider(source:source,credentials:credentials); case .stalker: return StalkerProvider(source:source,credentials:credentials) }
}
