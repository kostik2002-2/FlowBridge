import AppKit
import WebKit

// Tests the unchanged adapter in a real WebKit JS engine at document_start.
// This deliberately does not claim to test Safari's extension permissions or Google Flow.
final class Check: NSObject, WKNavigationDelegate {
    var view: WKWebView!
    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        webView.evaluateJavaScript("window.fixtureResult") { value, error in
            if let error = error { print("WebKit evaluation failed: \(error)"); exit(1) }
            guard let result = value as? [String: Any],
                  result["country"] as? Bool == true,
                  result["age"] as? Bool == false,
                  result["afterDispose"] as? Bool == false,
                  result["originalEvaluated"] as? Bool == true else {
                print("WebKit adapter fixture failed: \(String(describing: value))"); exit(1)
            }
            print("PASS: real macOS WebKit document_start fixture changes country only, evaluates original, and restores on disposal")
            exit(0)
        }
    }
    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        print("WebKit fixture failed: \(error)"); exit(1)
    }
}

let source = try String(contentsOfFile: CommandLine.arguments[1], encoding: .utf8)
let bootstrap = "(() => { const module = {exports:{}};\n" + source + "\nwindow.flowBridgeFixtureAdapter = module.exports.attachAdapter(window, () => {}); })();"
let config = WKWebViewConfiguration()
config.userContentController.addUserScript(WKUserScript(source: bootstrap, injectionTime: .atDocumentStart, forMainFrameOnly: true))
let app = NSApplication.shared
app.setActivationPolicy(.prohibited)
let check = Check()
check.view = WKWebView(frame: .zero, configuration: config)
check.view.navigationDelegate = check
let html = try String(contentsOfFile: CommandLine.arguments[2], encoding: .utf8)
check.view.loadHTMLString(html, baseURL: URL(string: "https://flow.google.com/fixture"))
Timer.scheduledTimer(withTimeInterval: 45, repeats: false) { _ in
    print("WebKit fixture timed out; no interactive Safari compatibility result is available")
    exit(1)
}
app.run()
