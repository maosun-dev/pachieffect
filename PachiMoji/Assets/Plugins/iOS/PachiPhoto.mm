// 脳汁メーカーの写真選び（iPhone 用の部品）。
// PHPicker で写真を1枚選び、長い辺を maxSide 以下に縮め、向きを正しく直して JPEG で outPath に写す。
// PHPicker は写真のアプリの外で動くので、写真へのアクセスの許可は聞かない（Info.plist の文も要らない）。
// 大きい写真（4800 万画素・HEIC など）でもメモリを食わないよう、元の画像は読み込まず ImageIO の縮小で作る。
// 動画も選べる。動画は作り直さず（時間がかかるため）、そのまま outPath の拡張子を元の動画の拡張子にした場所へ写す。
// 向き（縦に撮った動画の回転）はアプリの中で回して合わせるので、回す角度（度）をいっしょに返す。
// C# 側は PhotoPicker.cs。返事は UnitySendMessage("PachiPhotoBridge", "OnPicked", "ok"|"video:<秒>:<角度>:<拡張子>"|"cancel"|"failed")。
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

// 動画の長さ（秒）と、表示するときに回す角度（0・90・180・270 度、時計回り）
static void PachiVideoInfo(NSString* path, double* sec, int* deg)
{
    AVURLAsset* a = [AVURLAsset URLAssetWithURL:[NSURL fileURLWithPath:path] options:nil];
    *sec = CMTimeGetSeconds(a.duration);
    *deg = 0;
    AVAssetTrack* t = [[a tracksWithMediaType:AVMediaTypeVideo] firstObject];
    if (t == nil) return;
    CGAffineTransform m = t.preferredTransform;
    int d = (int)lround(atan2(m.b, m.a) * 180.0 / M_PI);
    *deg = ((d % 360) + 360) % 360;
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
        // 渡される一時ファイルは、この中でしか使えないので、そのまま手元へ写す（作り直さない）
        [item loadFileRepresentationForTypeIdentifier:@"public.movie" completionHandler:^(NSURL* url, NSError* error) {
            if (url == nil || error != nil) { PachiPhotoReply(@"failed"); return; }
            NSString* ext = url.pathExtension.lowercaseString;
            if (ext.length == 0) ext = @"mov";
            NSString* dst = [[outPath stringByDeletingPathExtension] stringByAppendingPathExtension:ext];
            NSFileManager* fm = [NSFileManager defaultManager];
            [fm removeItemAtPath:dst error:nil];
            if (![fm copyItemAtURL:url toURL:[NSURL fileURLWithPath:dst] error:nil]) { PachiPhotoReply(@"failed"); return; }
            double sec = 0; int deg = 0;
            PachiVideoInfo(dst, &sec, &deg);
            if (sec <= 0) { [fm removeItemAtPath:dst error:nil]; PachiPhotoReply(@"failed"); return; }
            PachiPhotoReply([NSString stringWithFormat:@"video:%.3f:%d:%@", sec, deg, ext]);
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
