// 脳汁メーカーの広告の追跡の許可（ATT）。初回だけ iPhone の確認を出し、決まったら Unity に知らせる。
// C# 側は Ads.cs（返事は PachiTrackingBridge.OnTracking に届く）。許可されなくても広告は出す
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#include "UnityInterface.h"   // UnitySendMessage

static void PachiTrackingReply()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage("PachiTrackingBridge", "OnTracking", "");
    });
}

extern "C" void PachiTracking_Request(void)
{
    // すでに答えが決まっていれば聞かない
    if (ATTrackingManager.trackingAuthorizationStatus != ATTrackingManagerAuthorizationStatusNotDetermined)
    {
        PachiTrackingReply();
        return;
    }
    [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
        PachiTrackingReply();
    }];
}
