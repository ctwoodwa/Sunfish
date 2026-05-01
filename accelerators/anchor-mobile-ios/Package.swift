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
    ],
    dependencies: [
        // swift-crypto provides Ed25519 + HMAC parity with the .NET side per
        // ADR 0004. Apple-maintained; pinned to a stable 3.x.
        .package(url: "https://github.com/apple/swift-crypto.git", from: "3.0.0"),
    ],
    targets: [
        .target(
            name: "SunfishFieldIdentity",
            dependencies: [
                .product(name: "Crypto", package: "swift-crypto"),
            ],
            path: "Sources/Identity"
        ),
        .testTarget(
            name: "SunfishFieldIdentityTests",
            dependencies: ["SunfishFieldIdentity"],
            path: "Tests/SunfishFieldIdentityTests"
        ),
    ]
)
