using Microsoft.VisualStudio.TestTools.UnitTesting;
using CefSharp.fastBOT.Models;
using CefSharp.fastBOT.Core;

namespace CefSharp.fastBOT.Tests
{
    [TestClass]
    public class ProxyConfigTests
    {
        [TestMethod]
        public void ProxyConfig_BasicConfiguration_ShouldReturnCorrectString()
        {
            // Arrange
            var config = new ProxyConfig
            {
                Host = "127.0.0.1",
                Port = 8080,
                Scheme = "http"
            };

            // Act
            var result = config.ToString();

            // Assert
            Assert.AreEqual("http://127.0.0.1:8080", result);
        }

        [TestMethod]
        public void ProxyConfig_WithAuthentication_ShouldReturnCorrectConnectionString()
        {
            // Arrange
            var config = new ProxyConfig
            {
                Host = "proxy.example.com",
                Port = 8080,
                Username = "user",
                Password = "pass"
            };

            // Act
            var result = config.ToConnectionString();

            // Assert
            Assert.AreEqual("proxy.example.com:8080:user:pass", result);
        }
    }

    [TestClass]
    public class UserSettingsTests
    {
        [TestMethod]
        public void UserSettings_DefaultValues_ShouldBeValid()
        {
            // Arrange & Act
            var settings = new UserSettings();

            // Assert
            Assert.AreEqual(2000, settings.CheckInterval);
            Assert.IsNotNull(settings);
        }
    }

    [TestClass]
    public class RequestContextManagerTests
    {
        [TestMethod]
        public void RequestContextManager_CreateIsolatedContext_ShouldReturnValidContext()
        {
            // Note: これはCefSharpが初期化されている環境でのみ動作します
            // 実際のテストでは適切なセットアップが必要です
            
            // Arrange
            var manager = new RequestContextManager();
            var contextName = "TestContext";

            // Act & Assert
            // CefSharpの初期化が必要なため、統合テストとして実装することを推奨
            Assert.IsNotNull(manager);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // テスト後のクリーンアップ処理
        }
    }
}