using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
namespace FocusSearch.Tests
{
    internal static class AppIndexTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(IndexFiltersAndDeduplicates); tests.Add(ShortcutTargetsDetermineApplicationMembership); }
        private static void ShortcutTargetsDetermineApplicationMembership()
        {
            string root = Path.Combine(Path.GetTempPath(), "FocusShortcutTypes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string folder = Path.Combine(root, "Note folder.exe");
                Directory.CreateDirectory(folder);
                string document = Path.Combine(root, "Note document.txt");
                File.WriteAllText(document, "fixture");
                CreateShortcut(Path.Combine(root, "Note app.lnk"), Path.Combine(Environment.SystemDirectory, "cmd.exe"));
                CreateShortcut(Path.Combine(root, "Note folder.lnk"), folder);
                CreateShortcut(Path.Combine(root, "Note document.lnk"), document);
                CreateShortcut(Path.Combine(root, "Note explorer folder.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + folder + "\"");
                CreateShortcut(Path.Combine(root, "Note missing app.lnk"), Path.Combine(root, "missing.exe"));
                CreateShortcut(Path.Combine(root, "Note administrative folder.lnk"), Path.Combine(Environment.SystemDirectory, "control.exe"), "/name Microsoft.AdministrativeTools");
                CreateShortcut(Path.Combine(root, "Note help document.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "hh.exe"), "manual.chm");
                File.WriteAllText(Path.Combine(folder, "Note build helper.exe"), "build artifact");
                File.WriteAllText(Path.Combine(root, "Note website.url"), "[InternetShortcut]\r\nURL=https://example.com\r\n");
                File.WriteAllText(Path.Combine(root, "Note unresolved.lnk"), "broken shortcut");
                File.WriteAllText(Path.Combine(root, "Note clickonce.appref-ms"), "fixture");
                using (var index = new AppIndex(new[] { root }))
                {
                    index.Refresh();
                    TestRunner.Equal(ResultKind.Application, index.Items.Single(i => i.Name == "Note app").Kind, "Executable shortcuts must remain applications.");
                    TestRunner.Equal(ResultKind.Folder, index.Items.Single(i => i.Name == "Note folder").Kind, "Folder shortcuts must not be mislabeled as applications, even with an executable-looking folder name.");
                    TestRunner.Equal(ResultKind.File, index.Items.Single(i => i.Name == "Note document").Kind, "Document shortcuts must be searchable as files.");
                    TestRunner.Equal(ResultKind.File, index.Items.Single(i => i.Name == "Note unresolved").Kind, "Unresolved shortcuts must not be presumed to be software.");
                    TestRunner.Equal(ResultKind.Folder, index.Items.Single(i => i.Name == "Note explorer folder").Kind, "Explorer folder launchers must not count as applications.");
                    TestRunner.Equal(ResultKind.File, index.Items.Single(i => i.Name == "Note missing app").Kind, "Missing executable targets must not appear as installed software.");
                    TestRunner.Equal(ResultKind.Folder, index.Items.Single(i => i.Name == "Note administrative folder").Kind, "The Administrative Tools folder must not count as an application.");
                    TestRunner.Equal(ResultKind.File, index.Items.Single(i => i.Name == "Note help document").Kind, "Help manuals opened through hh.exe must count as documents.");
                    TestRunner.Equal(false, index.Items.Any(i => i.Name == "Note build helper"), "Recursing desktop folders must not collect executable build artifacts as applications.");
                    var engine = new SearchEngine(index, new LocalFileProvider(new[] { root }), new Dictionary<string, UsageRecord>()) { FileSearchEnabled = false };
                    TestRunner.Equal(2, engine.Recent(10000).Count, "Only executable and ClickOnce launchers belong in the app grid.");
                    TestRunner.Equal(10, index.Search("note", 20).Count, "Filtering the grid must retain shortcuts in the searchable index.");
                    TestRunner.Equal(8, engine.SearchAsync("note", 8, CancellationToken.None).GetAwaiter().GetResult().Count, "Spotlight must retain non-app shortcuts within its result limit.");
                }
            }
            finally { Directory.Delete(root, true); }
        }
        private static void CreateShortcut(string path, string target, string arguments = "")
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            object shortcut = null;
            try
            {
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                shortcut.GetType().InvokeMember("TargetPath", BindingFlags.PutDispProperty, null, shortcut, new object[] { target });
                shortcut.GetType().InvokeMember("Arguments", BindingFlags.PutDispProperty, null, shortcut, new object[] { arguments });
                shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);
            }
        }
        private static void IndexFiltersAndDeduplicates()
        {
            string root = Path.Combine(Path.GetTempPath(), "FocusIndex-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "Notes.lnk"), "broken shortcut");
                File.WriteAllText(Path.Combine(root, "ignored.txt"), "text");
                Directory.CreateDirectory(Path.Combine(root, "copy"));
                File.WriteAllText(Path.Combine(root, "copy", "Notes.lnk"), "broken shortcut");
                using (var index = new AppIndex(new[] { root, root, Path.Combine(root, "missing") }))
                {
                    index.Refresh();
                    TestRunner.Equal(2, index.Items.Count, "Distinct same-named shortcuts must be preserved; duplicate roots must not duplicate paths.");
                    TestRunner.Equal(2, index.Search("notes", 8).Count, "Name search must retain both distinct paths.");
                    TestRunner.Equal(0, index.Search("calculator", 8).Count, "Unmatched items must be excluded.");
                }
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
