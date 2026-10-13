#import <UIKit/UIKit.h>

extern "C" void LoopMasterNavigationHaptic()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 10.0, *))
        {
            static UISelectionFeedbackGenerator *generator = nil;
            if (generator == nil)
                generator = [[UISelectionFeedbackGenerator alloc] init];
            [generator selectionChanged];
            [generator prepare];
        }
    });
}
