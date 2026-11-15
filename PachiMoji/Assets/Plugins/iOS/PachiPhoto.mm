// 脳汁メーカーの写真選び（iPhone 用の部品）。
// PHPicker で写真を1枚選び、長い辺を maxSide 以下に縮め、向きを正しく直して JPEG で outPath に写す。
// PHPicker は写真のアプリの外で動くので、写真へのアクセスの許可は聞かない（Info.plist の文も要らない）。
// 大きい写真（4800 万画素・HEIC など）でもメモリを食わないよう、元の画像は読み込まず ImageIO の縮小で作る。
// 動画も選べる。動画は向きを正しく直し、長い辺 1920 までの mp4 にして outPath の拡張子を .mp4 にした場所へ写す（音もそのまま）。
// C# 側は PhotoPicker.cs。返事は UnitySendMessage("PachiPhotoBridge", "OnPicked", "ok"|"video:<秒>"|"cancel"|"failed")。
#import <UIKit/UIKit.h>
#import <PhotosUI/PhotosUI.h>
#import <ImageIO/ImageIO.h>
#import <AVFoundation/AVFoundation.h>
#include "UnityInterface.h"   // UnitySendMessage・UnityGetGLViewController

static void PachiPhotoReply(NSString* result)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage("PachiPhotoBridge", "OnPicked", result.UTF8String);
    });
}

// 画像のファイルを縮めて（向きも直して）JPEG で書く
static BOOL PachiPhotoWrite(NSURL* src, NSString* outPath, int maxSide)
{
    CGImageSourceRef source = CGImageSourceCreateWithURL((__bridge CFURLRef)src, NULL);
    if (source == NULL) return NO;
    NSDictionary* opt = @{
        (id)kCGImageSourceCreateThumbnailFromImageAlways: @YES,
        (id)kCGImageSourceCreateThumbnailWithTransform: @YES,   // 写真の向き（EXIF）を正しく直す
        (id)kCGImageSourceThumbnailMaxPixelSize: @(maxSide),
        (id)kCGImageSourceShouldCacheImmediately: @YES,
    };
    CGImageRef img = CGImageSourceCreateThumbnailAtIndex(source, 0, (__bridge CFDictionaryRef)opt);
    CFRelease(source);
    if (img == NULL) return NO;
    UIImage* ui = [UIImage imageWithCGImage:img];
    CGImageRelease(img);
    NSData* jpg = UIImageJPEGRepresentation(ui, 0.9);
    if (jpg == nil) return NO;
    return [jpg writeToFile:outPath atomically:YES];
}

// 動画を、向きを直して（回転の情報を絵に焼き込んで）長い辺 1920 までの mp4 に写す。終わったら長さ（秒）を返す（失敗は -1）
static void PachiVideoImport(NSURL* src, NSString* dst, void (^done)(double))
{
    AVURLAsset* asset = [AVURLAsset URLAssetWithURL:src options:nil];
    AVAssetExportSession* ex = [[AVAssetExportSession alloc] initWithAsset:asset presetName:AVAssetExportPreset1920x1080];
    if (ex == nil) { done(-1); return; }
    [[NSFileManager defaultManager] removeItemAtPath:dst error:nil];
    ex.outputURL = [NSURL fileURLWithPath:dst];
    ex.outputFileType = AVFileTypeMPEG4;
    ex.videoComposition = [AVMutableVideoComposition videoCompositionWithPropertiesOfAsset:asset];   // 向きを正しく直す
    ex.shouldOptimizeForNetworkUse = NO;
    [ex exportAsynchronouslyWithCompletionHandler:^{
        if (ex.status != AVAssetExportSessionStatusCompleted) { done(-1); return; }
        AVURLAsset* out = [AVURLAsset URLAssetWithURL:[NSURL fileURLWithPath:dst] options:nil];
        done(CMTimeGetSeconds(out.duration));
    }];
}

@interface PachiPhotoDelegate : NSObject <PHPickerViewControllerDelegate>
@property (nonatomic, copy) NSString* outPath;
@property (nonatomic) int maxSide;
@end

static PachiPhotoDelegate* gPhotoDelegate;   // 選び終わるまで持っておく

@implementation PachiPhotoDelegate
- (void)picker:(PHPickerViewController*)picker didFinishPicking:(NSArray<PHPickerResult*>*)results
{
    [picker dismissViewControllerAnimated:YES completion:nil];
    NSString* outPath = self.outPath;
    int maxSide = self.maxSide;
    gPhotoDelegate = nil;
    if (results.count == 0) { PachiPhotoReply(@"cancel"); return; }
    NSItemProvider* item = results.firstObject.itemProvider;
    if ([item hasItemConformingToTypeIdentifier:@"public.movie"])
    {
        NSString* dst = [[outPath stringByDeletingPathExtension] stringByAppendingPathExtension:@"mp4"];
        // 渡される一時ファイルは、この中でしか使えないので、先に手元へ写してから変換する
        [item loadFileRepresentationForTypeIdentifier:@"public.movie" completionHandler:^(NSURL* url, NSError* error) {
            if (url == nil || error != nil) { PachiPhotoReply(@"failed"); return; }
            NSString* tmp = [NSTemporaryDirectory() stringByAppendingPathComponent:[@"pachi_pick_" stringByAppendingString:url.lastPathComponent]];
            [[NSFileManager defaultManager] removeItemAtPath:tmp error:nil];
            if (![[NSFileManager defaultManager] copyItemAtURL:url toURL:[NSURL fileURLWithPath:tmp] error:nil]) { PachiPhotoReply(@"failed"); return; }
            PachiVideoImport([NSURL fileURLWithPath:tmp], dst, ^(double sec) {
                [[NSFileManager defaultManager] removeItemAtPath:tmp error:nil];
                PachiPhotoReply(sec > 0 ? [NSString stringWithFormat:@"video:%.3f", sec] : @"failed");
            });
        }];
        return;
    }
    if (![item hasItemConformingToTypeIdentifier:@"public.image"]) { PachiPhotoReply(@"failed"); return; }
    // 渡される一時ファイルは、この中でしか使えない
    [item loadFileRepresentationForTypeIdentifier:@"public.image" completionHandler:^(NSURL* url, NSError* error) {
        BOOL ok = url != nil && error == nil && PachiPhotoWrite(url, outPath, maxSide);
        PachiPhotoReply(ok ? @"ok" : @"failed");
    }];
}
@end

extern "C" void PachiPhoto_Pick(const char* outPath, int maxSide)
{
    NSString* path = [NSString stringWithUTF8String:outPath];
    dispatch_async(dispatch_get_main_queue(), ^{
        PHPickerConfiguration* config = [[PHPickerConfiguration alloc] init];
        config.filter = [PHPickerFilter anyFilterMatchingSubfilters:@[[PHPickerFilter imagesFilter], [PHPickerFilter videosFilter]]];
        config.selectionLimit = 1;
        PHPickerViewController* picker = [[PHPickerViewController alloc] initWithConfiguration:config];
        gPhotoDelegate = [[PachiPhotoDelegate alloc] init];
        gPhotoDelegate.outPath = path;
        gPhotoDelegate.maxSide = maxSide;
        picker.delegate = gPhotoDelegate;
        UIViewController* vc = UnityGetGLViewController();
        if (vc == nil) { gPhotoDelegate = nil; PachiPhotoReply(@"failed"); return; }
        [vc presentViewController:picker animated:YES completion:nil];
    });
}
