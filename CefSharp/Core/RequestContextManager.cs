using System;
using System.Collections.Generic;
using CefSharp;

namespace CefSharp.fastBOT.Core
{
    public class RequestContextManager : IDisposable
    {
        private readonly Dictionary<string, IRequestContext> _contexts;
        private bool _disposed = false;

        public RequestContextManager()
        {
            _contexts = new Dictionary<string, IRequestContext>();
        }

        public IRequestContext CreateIsolatedContext(string name)
        {
            try
            {
                if (_contexts.ContainsKey(name))
                {
                    return _contexts[name];
                }

                var settings = new RequestContextSettings()
                {
                    CachePath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "fastBOT", "Context", name
                    ),
                    // PersistUserPreferences プロパティは削除されました
                    // 代わりに PersistSessionCookies と PersistUserPreferences が統合
                    AcceptLanguageList = "ja-JP,ja,en-US,en"
                };

                var context = new RequestContext(settings);
                _contexts[name] = context;

                Console.WriteLine($"RequestContext created: {name}");
                return context;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RequestContext creation failed: {ex.Message}");
                return null;
            }
        }

        public IRequestContext GetContext(string name)
        {
            _contexts.TryGetValue(name, out var context);
            return context;
        }

        public bool RemoveContext(string name)
        {
            if (_contexts.TryGetValue(name, out var context))
            {
                try
                {
                    context?.Dispose();
                    _contexts.Remove(name);
                    Console.WriteLine($"RequestContext removed: {name}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RequestContext removal failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        public IRequestContext GetDefaultContext()
        {
            return Cef.GetGlobalRequestContext();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var kvp in _contexts)
                {
                    try
                    {
                        kvp.Value?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing context {kvp.Key}: {ex.Message}");
                    }
                }
                _contexts.Clear();
                _disposed = true;
            }
        }
    }
}