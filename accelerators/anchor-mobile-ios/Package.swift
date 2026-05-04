// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "SunfishField",
    platforms: [
        .iOS(.v16),
        .macOS(.v13),
    ],
    products: [
        .library(
            name: "SunfishFieldIdentity",
            targets: ["SunfishFieldIdentity"]
        ),
        // Phase 1 scaffold — SwiftUI app shell. The real app builds via
        // Project.xcodeproj; this library product lets `swift build` verify
        // the SwiftUI sources compile without a full Xcode invocation.
        .library(
            name: "SunfishField",
            targets: ["SunfishField"]
        ),
    ],
    dependencies: [
        // swift-crypto: Ed25519 + HMAC parity with .NET per ADR 0004.
        .package(url: "https://github.com/apple/swift-crypto.git", from: "3.0.0"),
        // GRDB.swift: SQLite ORM for local persistence (Phase 2).
        // Declared here so Package.resolved pins the version before Phase 2
        // begins (per pre-release-latest-first policy).
        .package(url: "https://github.com/groue/GRDB.swift.git", from: "6.0.0"),
    ],
    targets: [
        .target(
            name: "SunfishFieldIdentity",
            dependencies: [
                .product(name: "Crypto", package: "swift-crypto"),
            ],
            path: "Sources/Identity"
        ),
        .target(
            name: "SunfishField",
            dependencies: [
                "SunfishFieldIdentity",
                // GRDB pinned in Package.resolved; Phase 2 adds the imports.
                .product(name: "GRDB", package: "GRDB.swift"),
            ],
            path: "SunfishField",
            // The SPM-vs-Xcode entry-point split is handled by a
            // `#if !SWIFT_PACKAGE` guard around the `@main` attribute in
            // SunfishFieldApp.swift, so the file is shared cleanly between
            // both build systems.
            exclude: ["Info.plist"]
        ),
        .testTarget(
            name: "SunfishFieldIdentityTests",
            dependencies: ["SunfishFieldIdentity"],
            path: "Tests/SunfishFieldIdentityTests"
        ),
        .testTarget(
            name: "SunfishFieldPersistenceTests",
            dependencies: [
                "SunfishField",
                .product(name: "GRDB", package: "GRDB.swift"),
            ],
            path: "Tests/SunfishFieldPersistenceTests"
        ),
        .testTarget(
            name: "SunfishFieldEventsTests",
            dependencies: ["SunfishField"],
            path: "Tests/SunfishFieldEventsTests"
        ),
    ]
)
