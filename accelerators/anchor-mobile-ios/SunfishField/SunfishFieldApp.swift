import SwiftUI

// `@main` is gated to Xcode builds only. The SPM library target compiles
// the same source file but skips the entry-point declaration so the
// `_main` symbol does not collide with the SPM test runner's `_main`.
#if !SWIFT_PACKAGE
@main
#endif
struct SunfishFieldApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}
