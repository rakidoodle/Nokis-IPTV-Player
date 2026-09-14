import SwiftUI
import AppKit

@main struct NokiIPTVApp:App {
    @StateObject private var library=Library()
    @StateObject private var playback=Playback()
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    var body:some Scene {
        WindowGroup("Noki’s IPTV Player for MacOS") {
            RootView(library:library,playback:playback).onAppear {
                if CommandLine.arguments.contains("--qa-light") { NSApp.appearance=NSAppearance(named:.aqua) }
                NSApp.setActivationPolicy(.regular); NSApp.activate(ignoringOtherApps:true)
                if let window=NSApp.windows.first { window.setContentSize(NSSize(width:1120,height:800)); window.center(); window.acceptsMouseMovedEvents=true; window.collectionBehavior.insert(.fullScreenPrimary) }
                if let index=CommandLine.arguments.firstIndex(of:"--smoke-test"),CommandLine.arguments.count > index+1 { SmokeTest.run(playback:playback,directory:URL(fileURLWithPath:CommandLine.arguments[index+1])) }
            }
        }.windowStyle(.titleBar).commands {
            CommandGroup(replacing:.newItem) {}
            CommandMenu("Playback") {
                Button("Play / Pause") { playback.togglePause() }.keyboardShortcut("p",modifiers:[.command])
                Button("Fullscreen") { playback.toggleFullscreen() }.keyboardShortcut("f",modifiers:[.command,.control])
                Button("Load Subtitles…") { playback.importSubtitle() }.disabled(playback.item == nil)
                Button("Stop") { playback.stop() }
            }
        }
    }
}
final class AppDelegate:NSObject,NSApplicationDelegate {
    func applicationShouldTerminateAfterLastWindowClosed(_ sender:NSApplication)->Bool { true }
}
