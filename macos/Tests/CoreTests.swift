import Foundation

final class FixtureTransport:HTTPTransport {
    var handler:(URLRequest)throws->Any
    init(_ handler:@escaping (URLRequest)throws->Any) { self.handler=handler }
    func data(for request:URLRequest) async throws -> Data { try JSONSerialization.data(withJSONObject:handler(request),options:.fragmentsAllowed) }
}
@main struct CoreTests {
    static var passed=0
    static func check(_ test:Bool,_ label:String) { if !test { print("FAIL: \(label)"); exit(1) }; passed += 1; print("PASS: \(label)") }
    static func main() async throws {
        let playlist="""
        #EXTM3U x-tvg-url="https://guide.example/epg.xml.gz"
        #EXTINF:-1 tvg-id="news" group-title="News, World" tvg-logo="https://img.example/a.png",News HD
        #EXTVLCOPT:http-user-agent=Noki Test
        https://streams.example/live.ts
        #EXTINF:-1 group-title="Cinema",A Film
        movie.mp4|Referer=https%3A%2F%2Fexample.com
        """
        let c=try M3UParser.parse(playlist,sourceID:"a",base:URL(string:"https://streams.example/list.m3u"))
        check(c.items.count==2,"M3U entries")
        check(c.items[0].name=="News HD" && c.items[0].group=="News, World","Quoted comma metadata")
        check(c.items[0].headers["User-Agent"]=="Noki Test","M3U request headers")
        check(c.items[1].location=="https://streams.example/movie.mp4" && c.items[1].kind == .movie,"Relative VOD URL")
        check(c.items[1].headers["Referer"]=="https://example.com","URL header decoding")
        check(c.epgURL.hasSuffix("epg.xml.gz"),"EPG discovery")
        do { _=try M3UParser.parse("<html>bad</html>",sourceID:"a"); check(false,"Invalid M3U rejected") } catch { check(true,"Invalid M3U rejected") }
        let hls=try M3UParser.parse("#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXTINF:10,\npart.ts",sourceID:"a",base:URL(string:"https://s.example/live.m3u8"))
        check(hls.items.count==1 && hls.items[0].location.hasSuffix("live.m3u8"),"HLS preserved as one stream")
        let xml="""
        <tv><programme channel="news" start="20260909160000 +0800" stop="20260909170000 +0800"><title>News &amp; Weather</title><desc>Today</desc></programme></tv>
        """
        let epg=try XMLTVParser.parse(Data(xml.utf8))
        check(epg.count==1 && epg[0].title=="News & Weather","XMLTV text and channel")
        check(epg[0].start==XMLTVParser.date("20260909080000 +0000"),"EPG timezone conversion")
        do { _=try XMLTVParser.parse(Data("<tv><broken>".utf8)); check(false,"Malformed XML rejected") } catch { check(true,"Malformed XML rejected") }
        let source=Source(id:"xc",name:"Test",kind:.xtream)
        let credentials=Credentials(endpoint:"https://test.example",username:"user+name",password:"secret&value")
        let transport=FixtureTransport { request in
            let q=URLComponents(url:request.url!,resolvingAgainstBaseURL:false)!.queryItems!
            let p=Dictionary(uniqueKeysWithValues:q.map { ($0.name,$0.value ?? "") })
            check(p["password"]=="secret&value","Credentials URL encoded")
            switch p["action"] ?? "" {
            case "": return ["user_info":["auth":1,"status":"Active"]]
            case "get_live_categories","get_vod_categories","get_series_categories":return [["category_id":"1","category_name":"Test group"]]
            case "get_live_streams":return [["stream_id":1,"name":"Live","category_id":"1","epg_channel_id":"news"]]
            case "get_vod_streams":return [["stream_id":2,"name":"Movie","category_id":"1","container_extension":"mkv"]]
            case "get_series":return [["series_id":3,"name":"Series","category_id":"1"]]
            case "get_series_info":return ["episodes":["2":[["id":"9","title":"Episode 1","episode_num":1,"container_extension":"mp4"]]]]
            default:return []
            }
        }
        let provider=XtreamProvider(source:source,credentials:credentials,transport:transport)
        let cat=try await provider.catalog(); check(cat.items.count==3,"Xtream three catalogs")
        check(cat.items[1].location.hasSuffix("2.mkv"),"Xtream container extension")
        let episodes=try await provider.episodes(for:cat.items[2]); check(episodes.count==1 && episodes[0].season==2,"Xtream episode grouping")
        let bad=XtreamProvider(source:source,credentials:credentials,transport:FixtureTransport { _ in ["user_info":["auth":0]] })
        do { _=try await bad.catalog(); check(false,"Xtream rejects bad login") } catch { check(!error.localizedDescription.contains("secret"),"Xtream rejects bad login without secret") }
        var handshakes=0; var failOnce=true
        let st=FixtureTransport { request in
            let p=Dictionary(uniqueKeysWithValues:URLComponents(url:request.url!,resolvingAgainstBaseURL:false)!.queryItems!.map { ($0.name,$0.value ?? "") })
            switch p["action"] {
            case "handshake":handshakes += 1;return ["js":["token":"token\(handshakes)"]]
            case "get_profile":return ["js":["id":1]]
            case "get_genres","get_categories":return ["js":[["id":"1","title":"Test"]]]
            case "get_all_channels":
                if failOnce { failOnce=false; throw PlayerError.message("Authentication was rejected.") };return ["js":["data":[["id":"1","name":"Live","cmd":"ffmpeg http://localhost/live","tv_genre_id":"1"]]]]
            case "get_ordered_list":
                if p["type"]=="series" { return ["js":["data":[],"total_items":0]] }
                let page=Int(p["p"] ?? "1")!;return ["js":["data":[["id":"\(page)","name":"Film \(page)","cmd":"ffmpeg http://localhost/\(page)"]],"total_items":2,"max_page_items":1]]
            case "create_link":return ["js":["cmd":"ffmpeg https://stream.example/live.ts"]]
            case "get_epg_info":return ["js":["data":["1":[["name":"Now","start_timestamp":"1788940800","stop_timestamp":"1788944400"]]]]]
            default:return ["js":[:]]
            }
        }
        let stalker=StalkerProvider(source:Source(id:"st",name:"Stalker",kind:.stalker),credentials:Credentials(endpoint:"https://portal.example/stalker_portal/c/",mac:"00:1A:79:12:34:56"),transport:st)
        let sc=try await stalker.catalog(); check(sc.items.count==3,"Stalker paginated catalog")
        check(handshakes==2,"Stalker renews expired session once")
        let link=try await stalker.resolve(sc.items[0]); check(link.url.absoluteString=="https://stream.example/live.ts","Stalker resolves portal command")
        let sg=try await stalker.guide(for:sc.items,explicitURL:"");check(sg.count==1,"Stalker guide endpoint")
        let dir=FileManager.default.temporaryDirectory.appendingPathComponent("noki-tests-"+UUID().uuidString); defer { try? FileManager.default.removeItem(at:dir) }
        let disk=DiskStore(directory:dir); let snapshot=LibrarySnapshot(sources:[source],favorites:["xc:movie:2"]); try disk.save(snapshot)
        let loaded=try disk.load();check(loaded.favorites==snapshot.favorites && loaded.sources==snapshot.sources,"Favorites and sources persist")
        let saved=try String(contentsOf:dir.appendingPathComponent("library.json"),encoding:.utf8);check(!saved.contains("secret&value"),"Library metadata excludes credentials")
        var health=PlaybackHealth(now:0)
        check(health.evaluate(now:0,time:0,frames:0,paused:false,failed:false) == .buffering,"Startup shows buffering")
        check(health.evaluate(now:1,time:1000,frames:25,paused:false,failed:false) == .healthy,"Progress clears buffering")
        check(health.evaluate(now:4,time:1000,frames:25,paused:false,failed:false) == .buffering,"Short stall waits without restarting")
        check(health.evaluate(now:9,time:1000,frames:25,paused:false,failed:false) == .reconnect,"Eight-second stall requests automatic reconnect")
        health=PlaybackHealth(now:0)
        _=health.evaluate(now:1,time:1000,frames:25,paused:false,failed:false)
        check(health.evaluate(now:60,time:1000,frames:25,paused:true,failed:false) == .healthy,"Intentional pause never reconnects")
        health=PlaybackHealth(now:0)
        _=health.evaluate(now:1,time:1000,frames:25,paused:false,failed:false)
        for time in 2...8 { _=health.evaluate(now:Double(time),time:Int32(time*1000),frames:25,paused:false,failed:false) }
        check(health.evaluate(now:9,time:9000,frames:25,paused:false,failed:false) == .reconnect,"Frozen video detected even while audio clock advances")
        health=PlaybackHealth(now:0)
        var audioAction=PlaybackHealth.Action.buffering
        for time in 1...12 { audioAction=health.evaluate(now:Double(time),time:Int32(time*1000),frames:0,paused:false,failed:false) }
        check(audioAction == .healthy,"Audio-only streams do not trigger false frame-stall detection")
        health=PlaybackHealth(now:0)
        _=health.evaluate(now:0,time:0,frames:0,paused:false,failed:false)
        check(health.evaluate(now:20,time:0,frames:0,paused:false,failed:false) == .reconnect,"Unstarted stream has bounded startup timeout")
        health=PlaybackHealth(now:0)
        for time in 1...32 { _=health.evaluate(now:Double(time),time:Int32(time*1000),frames:Int32(time*25),paused:false,failed:false) }
        check(health.stable(at:32),"Healthy playback resets recovery budget after 30 seconds")
        health.didSeek(now:40)
        check(health.evaluate(now:41,time:32000,frames:800,paused:false,failed:false) == .healthy,"Seeking gets a fresh stall grace period")
        check(health.evaluate(now:42,time:32000,frames:800,paused:false,failed:true) == .reconnect,"Playback errors request reconnect immediately")
        health=PlaybackHealth(now:0)
        var liveAction=PlaybackHealth.Action.buffering
        for time in 1...32 { liveAction=health.evaluate(now:Double(time),time:0,frames:Int32(time*25),paused:false,failed:false) }
        check(liveAction == .healthy,"Live video without a playback clock uses frame progress")
        print("\n\(passed) checks passed")
    }
}
