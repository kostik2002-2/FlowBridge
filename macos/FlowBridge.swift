import AppKit
import SafariServices

struct BrowserChoice {
    let name: String
    let bundleID: String
    let kind: String
    let manager: String
}

final class Installer: NSObject, NSApplicationDelegate {
    static let choices = [
        BrowserChoice(name: "Safari", bundleID: "com.apple.Safari", kind: "safari", manager: ""),
        BrowserChoice(name: "Google Chrome", bundleID: "com.google.Chrome", kind: "chromium", manager: "chrome://extensions/"),
        BrowserChoice(name: "Comet", bundleID: "ai.perplexity.comet", kind: "chromium", manager: "chrome://extensions/"),
        BrowserChoice(name: "Microsoft Edge", bundleID: "com.microsoft.edgemac", kind: "chromium", manager: "edge://extensions/"),
        BrowserChoice(name: "Opera", bundleID: "com.operasoftware.Opera", kind: "chromium", manager: "opera://extensions/"),
        BrowserChoice(name: "Яндекс Браузер", bundleID: "ru.yandex.desktop.yandex-browser", kind: "chromium", manager: "browser://extensions/"),
        BrowserChoice(name: "Mozilla Firefox", bundleID: "org.mozilla.firefox", kind: "firefox", manager: "")
    ]
    private var installed: [(BrowserChoice, URL)] = []
    private var window: NSWindow!
    private let browserList = NSPopUpButton()
    private let installButton = NSButton(title: "Установить", target: nil, action: nil)
    private let status = NSTextField(wrappingLabelWithString: "")

    func applicationDidFinishLaunching(_ notification: Notification) {
        installed = Self.choices.compactMap { choice in
            guard let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: choice.bundleID),
                  Bundle(url: url)?.bundleIdentifier == choice.bundleID else { return nil }
            return (choice, url)
        }
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 390, height: 300), styleMask: [.titled, .closable, .miniaturizable], backing: .buffered, defer: false)
        window.title = "FlowBridge Mac Preview"
        window.center()
        let heading = NSTextField(labelWithString: "FlowBridge")
        heading.font = .boldSystemFont(ofSize: 25)
        let label = NSTextField(labelWithString: "Выберите браузер")
        browserList.addItems(withTitles: installed.map { $0.0.name })
        browserList.setAccessibilityLabel("Выберите установленный браузер")
        let notice = NSTextField(wrappingLabelWithString: "Перед установкой включите VPN.\nТестовая версия: подтвердите установку в браузере.")
        notice.textColor = .secondaryLabelColor
        installButton.target = self
        installButton.action = #selector(install)
        installButton.bezelStyle = .rounded
        status.font = .systemFont(ofSize: 12)
        status.setAccessibilityRole(.staticText)
        let stack = NSStackView(views: [heading, label, browserList, notice, installButton, status])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 13
        stack.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 24),
            stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -24),
            stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 24),
            browserList.widthAnchor.constraint(equalTo: stack.widthAnchor),
            installButton.widthAnchor.constraint(equalTo: stack.widthAnchor)
        ])
        if installed.isEmpty { installButton.isEnabled = false; status.stringValue = "Поддерживаемые браузеры не найдены" }
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }

    private func copyResource(_ name: String, to target: URL) throws {
        guard let source = Bundle.main.resourceURL?.appendingPathComponent(name),
              FileManager.default.fileExists(atPath: source.path) else {
            throw NSError(domain: "FlowBridge", code: 1, userInfo: [NSLocalizedDescriptionKey: "В сборке отсутствует ресурс \(name)"])
        }
        let fm = FileManager.default
        try fm.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
        // Never overwrite a live unpacked-extension directory; stage each build separately.
        if fm.fileExists(atPath: target.path) { return }
        try fm.copyItem(at: source, to: target)
    }

    private func open(_ url: URL, in app: URL) {
        let configuration = NSWorkspace.OpenConfiguration()
        configuration.activates = true
        NSWorkspace.shared.open([url], withApplicationAt: app, configuration: configuration) { _, error in
            if let error = error { DispatchQueue.main.async { self.status.stringValue = error.localizedDescription } }
        }
    }

    @objc private func install() {
        guard installed.indices.contains(browserList.indexOfSelectedItem) else { return }
        let (browser, app) = installed[browserList.indexOfSelectedItem]
        installButton.isEnabled = false
        defer { installButton.isEnabled = true }
        do {
            let fm = FileManager.default
            let build = Bundle.main.infoDictionary?["CFBundleVersion"] as? String ?? "preview"
            let base = fm.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
                .appendingPathComponent("FlowBridge/mac-preview/\(build)", isDirectory: true)
            if browser.kind == "firefox" {
                let xpi = base.appendingPathComponent("FlowBridge-0.1.1-signed.xpi")
                try copyResource("FlowBridge-0.1.1-signed.xpi", to: xpi)
                open(xpi, in: app)
                status.stringValue = "Подтвердите установку в Firefox"
                explain("Firefox", "Подтвердите добавление FlowBridge Preview. После установки нажмите значок расширения → «Открыть Flow». Подписанный пакет Mozilla сохранён.")
            } else if browser.kind == "safari" {
                let target = fm.homeDirectoryForCurrentUser.appendingPathComponent("Applications/FlowBridge Safari Preview.app")
                let safariContainer = Bundle.main.resourceURL!.appendingPathComponent("FlowBridge Safari Preview.app")
                if fm.fileExists(atPath: safariContainer.path) {
                    // A fixed installed container must never be silently replaced while Safari uses it.
                    try copyResource("FlowBridge Safari Preview.app", to: target)
                    NSWorkspace.shared.openApplication(at: target, configuration: NSWorkspace.OpenConfiguration()) { _, error in
                        DispatchQueue.main.async {
                            if let error = error { self.status.stringValue = error.localizedDescription; return }
                            self.status.stringValue = "Включите расширение в настройках Safari"
                            self.explain("Safari — тестовая установка", "В Safari разрешите неподписанные расширения для разработки, затем включите FlowBridge и доступ к flow.google.com. После включения откройте значок расширения → «Открыть Flow». Разрешение неподписанных расширений сбрасывается при выходе из Safari.")
                        }
                    }
                } else {
                    let folder = base.appendingPathComponent("safari", isDirectory: true)
                    try copyResource("safari", to: folder)
                    NSWorkspace.shared.open(app)
                    status.stringValue = "Добавьте временное расширение в Safari"
                    explain("Safari 18.4+ — временный тест", "Safari → Настройки → Дополнения → функции для веб-разработчиков. Затем Разработчик → Add Temporary Extension. Выберите папку по кнопке ниже, разрешите доступ к Flow. Временное расширение удаляется после выхода из Safari или через 24 часа.", copyPath: folder.path)
                }
            } else {
                let folder = base.appendingPathComponent("chromium", isDirectory: true)
                try copyResource("chromium", to: folder)
                open(URL(string: browser.manager)!, in: app)
                status.stringValue = "Подтвердите установку в браузере"
                explain(browser.name, "Включите режим разработчика и нажмите «Загрузить распакованное расширение». В выборе папки нажмите ⌘⇧G и вставьте путь. После установки расширение откроет подготовку Flow.", copyPath: folder.path)
            }
        } catch { status.stringValue = "Не удалось установить: \(error.localizedDescription)" }
    }

    private func explain(_ title: String, _ message: String, copyPath: String? = nil) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: copyPath == nil ? "Понятно" : "Скопировать путь")
        if copyPath != nil { alert.addButton(withTitle: "Закрыть") }
        if alert.runModal() == .alertFirstButtonReturn, let path = copyPath {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(path, forType: .string)
        }
    }
}

if CommandLine.arguments.contains("--diagnose") {
    let rows = Installer.choices.compactMap { choice -> [String: String]? in
        guard let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: choice.bundleID),
              Bundle(url: url)?.bundleIdentifier == choice.bundleID else { return nil }
        return ["name": choice.name, "bundleID": choice.bundleID]
    }
    let data = try JSONSerialization.data(withJSONObject: rows, options: [.prettyPrinted, .sortedKeys])
    print(String(data: data, encoding: .utf8)!)
} else {
    let app = NSApplication.shared
    app.setActivationPolicy(.regular)
    let installer = Installer()
    app.delegate = installer
    app.run()
}
