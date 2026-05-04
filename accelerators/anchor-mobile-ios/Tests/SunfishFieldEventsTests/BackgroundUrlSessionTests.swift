import XCTest
import Foundation
@testable import SunfishField

final class BackgroundUrlSessionTests: XCTestCase {
    /// Verifies the 5 explicit configuration knobs from ADR 0028-A2.2 ship
    /// in the canonical `URLSessionConfiguration.background` produced by
    /// the factory. If any future commit accidentally drops one of these
    /// flags, this test fails — that's the trip-wire.
    func testConfiguration_Adr0028A22_ExplicitKnobsAllSet() {
        let config = BackgroundUrlSession.makeConfiguration()
        XCTAssertEqual(config.identifier, BackgroundUrlSession.identifier)
        XCTAssertEqual(config.identifier, "dev.sunfish.field.upload")
        XCTAssertFalse(config.isDiscretionary)
        XCTAssertTrue(config.sessionSendsLaunchEvents)
        XCTAssertTrue(config.allowsCellularAccess)
        XCTAssertTrue(config.waitsForConnectivity)
        XCTAssertEqual(config.timeoutIntervalForResource, 7 * 24 * 3600)
    }

    func testIdentifier_StableConstantString() {
        XCTAssertEqual(BackgroundUrlSession.identifier, "dev.sunfish.field.upload")
    }
}
