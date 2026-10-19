// 脳汁メーカーの動画の書き出し（iPhone 用の部品）。
// Unity が1コマずつ渡す BGRA の絵を、AVAssetWriter で mp4（H.264・無音）にする。
// あわせて、できた動画を写真に保存する・共有画面を出す。
// C# 側は IosVideoWriter.cs と VideoShare.cs。返す数は ExportError・WriteResult・FinishState の並びと同じ。
#import <AVFoundation/AVFoundation.h>
#import <Photos/Photos.h>
#import <UIKit/UIKit.h>
#include "UnityInterface.h"   // UnitySendMessage・UnityGetGLViewController

// ExportError と同じ数
enum { ErrNone = 0, ErrNoSpace = 1, ErrCannotOpen = 2, ErrWriteFailed = 3 };

// 書き出しは同時に1本だけなので、静的な変数で持つ
static AVAssetWriter* gWriter;
static AVAssetWriterInput* gInput;
static AVAssetWriterInputPixelBufferAdaptor* gAdaptor;
static NSString* gPath;
static int gW, gH, gFps;
static volatile int gState;   // 0 書いている・1 できた・2 失敗（FinishState と同じ）
static volatile int gError;   // ExportError の数

// 失敗の理由を見分ける：空き容量が無いのか、それ以外か
static int ErrorOf(NSError* e)
{
    for (NSError* x = e; x != nil; x = x.userInfo[NSUnderlyingErrorKey])
    {
        if (([x.domain isEqualToString:NSCocoaErrorDomain] && x.code == NSFileWriteOutOfSpaceError) ||
            ([x.domain isEqualToString:AVFoundationErrorDomain] && x.code == AVErrorDiskFull) ||
            ([x.domain isEqualToString:NSPOSIXErrorDomain] && x.code == ENOSPC))
            return ErrNoSpace;
    }
    return ErrWriteFailed;
}

static void Reset()
{
    gWriter = nil;
    gInput = nil;
    gAdaptor = nil;
}

static void RemoveFile()
{
    if (gPath != nil) [[NSFileManager defaultManager] removeItemAtPath:gPath error:nil];
}

static void ReplySaved(NSString* result)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage("PachiVideoBridge", "OnSaved", result.UTF8String);
    });
}

extern "C"
{
    long long PachiVideo_FreeBytes(const char* dir)
    {
        NSURL* url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:dir]];
        NSDictionary* v = [url resourceValuesForKeys:@[NSURLVolumeAvailableCapacityForImportantUsageKey] error:nil];
        NSNumber* n = v[NSURLVolumeAvailableCapacityForImportantUsageKey];
        return n != nil ? n.longLongValue : LLONG_MAX;   // わからないときは止めない（書いている途中の満杯は別に見分ける）
    }

    int PachiVideo_Begin(const char* path, int w, int h, int fps, int bitRate)
    {
        Reset();
        gPath = [NSString stringWithUTF8String:path];
        gW = w; gH = h; gFps = fps;
        gState = 0; gError = ErrNone;
        RemoveFile();

        NSError* err = nil;
        gWriter = [AVAssetWriter assetWriterWithURL:[NSURL fileURLWithPath:gPath] fileType:AVFileTypeMPEG4 error:&err];
        if (gWriter == nil) { Reset(); return ErrCannotOpen; }

        NSDictionary* settings = @{
            AVVideoCodecKey: AVVideoCodecTypeH264,
            AVVideoWidthKey: @(w),
            AVVideoHeightKey: @(h),
            AVVideoCompressionPropertiesKey: @{
                AVVideoAverageBitRateKey: @(bitRate),
                AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel,
                AVVideoExpectedSourceFrameRateKey: @(fps),
            },
        };
        gInput = [AVAssetWriterInput assetWriterInputWithMediaType:AVMediaTypeVideo outputSettings:settings];
        gInput.expectsMediaDataInRealTime = NO;
        gAdaptor = [AVAssetWriterInputPixelBufferAdaptor
            assetWriterInputPixelBufferAdaptorWithAssetWriterInput:gInput
            sourcePixelBufferAttributes:@{
                (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
                (id)kCVPixelBufferWidthKey: @(w),
                (id)kCVPixelBufferHeightKey: @(h),
            }];
        if (![gWriter canAddInput:gInput]) { Reset(); return ErrCannotOpen; }
        [gWriter addInput:gInput];
        if (![gWriter startWriting])
        {
            int e = ErrorOf(gWriter.error) == ErrNoSpace ? ErrNoSpace : ErrCannotOpen;
            Reset();
            RemoveFile();
            return e;
        }
        [gWriter startSessionAtSourceTime:kCMTimeZero];
        return ErrNone;
    }

    // 0 渡せた・1 まだ受け取れない（次のコマでもう一度）・2 失敗
    int PachiVideo_Append(const void* bgra, int frame)
    {
        if (gWriter == nil) { gError = ErrWriteFailed; return 2; }
        if (gWriter.status == AVAssetWriterStatusFailed) { gError = ErrorOf(gWriter.error); return 2; }
        if (!gInput.readyForMoreMediaData) return 1;

        CVPixelBufferRef buf = NULL;
        if (gAdaptor.pixelBufferPool == NULL ||
            CVPixelBufferPoolCreatePixelBuffer(NULL, gAdaptor.pixelBufferPool, &buf) != kCVReturnSuccess)
        {
            gError = ErrWriteFailed;
            return 2;
        }
        CVPixelBufferLockBaseAddress(buf, 0);
        uint8_t* dst = (uint8_t*)CVPixelBufferGetBaseAddress(buf);
        size_t dstRow = CVPixelBufferGetBytesPerRow(buf), srcRow = (size_t)gW * 4;
        const uint8_t* src = (const uint8_t*)bgra;
        // Unity の読み出しは下の行から、動画は上の行から並ぶので、上下を入れ替えて写す
        for (int y = 0; y < gH; y++)
            memcpy(dst + (size_t)y * dstRow, src + (size_t)(gH - 1 - y) * srcRow, srcRow);
        CVPixelBufferUnlockBaseAddress(buf, 0);

        BOOL ok = [gAdaptor appendPixelBuffer:buf withPresentationTime:CMTimeMake(frame, gFps)];
        CVPixelBufferRelease(buf);
        if (!ok) { gError = ErrorOf(gWriter.error); return 2; }
        return 0;
    }

    void PachiVideo_Finish(void)
    {
        if (gWriter == nil) { gError = ErrWriteFailed; gState = 2; return; }
        [gInput markAsFinished];
        AVAssetWriter* w = gWriter;
        [w finishWritingWithCompletionHandler:^{
            if (w.status == AVAssetWriterStatusCompleted) gState = 1;
            else { gError = ErrorOf(w.error); gState = 2; }
        }];
    }

    int PachiVideo_Poll(void) { return gState; }

    int PachiVideo_LastError(void) { return gError; }

    // 止めて、作りかけを消す
    void PachiVideo_Cancel(void)
    {
        if (gWriter != nil && gWriter.status == AVAssetWriterStatusWriting) [gWriter cancelWriting];
        Reset();
        RemoveFile();
    }

    // 写真に保存。初回だけ「追加のみ」の許可を聞く。返事は PachiVideoBridge.OnSaved に "ok"／"denied"／"failed"
    void PachiVideo_SaveToPhotos(const char* path)
    {
        NSURL* url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
        [PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelAddOnly handler:^(PHAuthorizationStatus st) {
            if (st != PHAuthorizationStatusAuthorized && st != PHAuthorizationStatusLimited) { ReplySaved(@"denied"); return; }
            [[PHPhotoLibrary sharedPhotoLibrary] performChanges:^{
                [PHAssetChangeRequest creationRequestForAssetFromVideoAtFileURL:url];
            } completionHandler:^(BOOL ok, NSError* e) {
                ReplySaved(ok ? @"ok" : @"failed");
            }];
        }];
    }

    // iPhone の共有画面（LINE・X・TikTok など）を出す
    void PachiVideo_Share(const char* path)
    {
        NSURL* url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
        dispatch_async(dispatch_get_main_queue(), ^{
            UIViewController* vc = UnityGetGLViewController();
            UIActivityViewController* a = [[UIActivityViewController alloc] initWithActivityItems:@[url] applicationActivities:nil];
            a.popoverPresentationController.sourceView = vc.view;
            a.popoverPresentationController.sourceRect = CGRectMake(vc.view.bounds.size.width / 2, vc.view.bounds.size.height, 1, 1);
            [vc presentViewController:a animated:YES completion:nil];
        });
    }
}
