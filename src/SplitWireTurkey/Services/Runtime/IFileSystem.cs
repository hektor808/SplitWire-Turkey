using System;
using System.IO;

namespace SplitWireTurkey.Services.Runtime
{
    public interface IFileSystem
    {
        bool FileExists(string path);
        string ReadAllText(string path);
        void AppendAllText(string path, string content);
        void CreateDirectory(string path);
        string Combine(params string[] paths);
        string GetLocalApplicationDataPath();
    }

    public sealed class FileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public void AppendAllText(string path, string content) => File.AppendAllText(path, content);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public string Combine(params string[] paths) => Path.Combine(paths);

        public string GetLocalApplicationDataPath()
            => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
}
