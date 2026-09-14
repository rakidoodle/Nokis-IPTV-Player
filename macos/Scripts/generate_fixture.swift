import Foundation
import AVFoundation
import AppKit
let dir=URL(fileURLWithPath:CommandLine.arguments[1]); try FileManager.default.createDirectory(at:dir,withIntermediateDirectories:true)
let out=dir.appendingPathComponent("sample.mp4"); try? FileManager.default.removeItem(at:out)
let writer=try AVAssetWriter(outputURL:out,fileType:.mp4)
let video=AVAssetWriterInput(mediaType:.video,outputSettings:[AVVideoCodecKey:AVVideoCodecType.h264,AVVideoWidthKey:640,AVVideoHeightKey:360])
let adaptor=AVAssetWriterInputPixelBufferAdaptor(assetWriterInput:video,sourcePixelBufferAttributes:[kCVPixelBufferPixelFormatTypeKey as String:kCVPixelFormatType_32ARGB,kCVPixelBufferWidthKey as String:640,kCVPixelBufferHeightKey as String:360])
writer.add(video)
let audio=AVAssetWriterInput(mediaType:.audio,outputSettings:[AVFormatIDKey:kAudioFormatMPEG4AAC,AVSampleRateKey:44100,AVNumberOfChannelsKey:1,AVEncoderBitRateKey:64000]); writer.add(audio)
writer.startWriting(); writer.startSession(atSourceTime:.zero)
let audioDone=DispatchSemaphore(value:0)
DispatchQueue.global().async {
var asbd=AudioStreamBasicDescription(mSampleRate:44100,mFormatID:kAudioFormatLinearPCM,mFormatFlags:kAudioFormatFlagIsSignedInteger|kAudioFormatFlagIsPacked,mBytesPerPacket:2,mFramesPerPacket:1,mBytesPerFrame:2,mChannelsPerFrame:1,mBitsPerChannel:16,mReserved:0)
var format:CMAudioFormatDescription?; CMAudioFormatDescriptionCreate(allocator:kCFAllocatorDefault,asbd:&asbd,layoutSize:0,layout:nil,magicCookieSize:0,magicCookie:nil,extensions:nil,formatDescriptionOut:&format)
for start in stride(from:0,to:882000,by:4410) {
    while !audio.isReadyForMoreMediaData { Thread.sleep(forTimeInterval:0.005) }
    var samples=(0..<4410).map { i in Int16(sin(Double(start+i)*2*Double.pi*440/44100)*2000) }
    var block:CMBlockBuffer?; CMBlockBufferCreateWithMemoryBlock(allocator:kCFAllocatorDefault,memoryBlock:nil,blockLength:8820,blockAllocator:kCFAllocatorDefault,customBlockSource:nil,offsetToData:0,dataLength:8820,flags:0,blockBufferOut:&block)
    samples.withUnsafeMutableBytes { raw in _ = CMBlockBufferReplaceDataBytes(with:raw.baseAddress!,blockBuffer:block!,offsetIntoDestination:0,dataLength:8820) }
    var sample:CMSampleBuffer?; CMAudioSampleBufferCreateReadyWithPacketDescriptions(allocator:kCFAllocatorDefault,dataBuffer:block!,formatDescription:format!,sampleCount:4410,presentationTimeStamp:CMTime(value:Int64(start),timescale:44100),packetDescriptions:nil,sampleBufferOut:&sample)
    audio.append(sample!)
}
audio.markAsFinished(); audioDone.signal()
}
for frame in 0..<600 {
    while !video.isReadyForMoreMediaData { Thread.sleep(forTimeInterval:0.005) }
    var pixel:CVPixelBuffer?; CVPixelBufferPoolCreatePixelBuffer(nil,adaptor.pixelBufferPool!,&pixel)
    let buffer=pixel!; CVPixelBufferLockBaseAddress(buffer,[])
    let ctx=CGContext(data:CVPixelBufferGetBaseAddress(buffer),width:640,height:360,bitsPerComponent:8,bytesPerRow:CVPixelBufferGetBytesPerRow(buffer),space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.noneSkipFirst.rawValue)!
    ctx.setFillColor(NSColor(calibratedHue:CGFloat(frame)/600,saturation:0.65,brightness:0.55,alpha:1).cgColor); ctx.fill(CGRect(x:0,y:0,width:640,height:360))
    ctx.setFillColor(NSColor.white.cgColor); ctx.fill(CGRect(x:(frame*3)%540,y:130,width:100,height:100))
    CVPixelBufferUnlockBaseAddress(buffer,[]); adaptor.append(buffer,withPresentationTime:CMTime(value:Int64(frame),timescale:30))
}
video.markAsFinished()
audioDone.wait(); let done=DispatchSemaphore(value:0); writer.finishWriting { done.signal() }; done.wait()
guard writer.status == .completed else { fatalError("Fixture failed") }
try "1\n00:00:00,000 --> 00:00:19,000\nNoki IPTV — subtitle test\n".write(to:dir.appendingPathComponent("sample.srt"),atomically:true,encoding:.utf8)
try "WEBVTT\n\n00:00:00.000 --> 00:00:19.000\nNoki IPTV — WebVTT test\n".write(to:dir.appendingPathComponent("sample.vtt"),atomically:true,encoding:.utf8)
print("Generated 20-second H.264/AAC media and SRT/VTT fixtures")
