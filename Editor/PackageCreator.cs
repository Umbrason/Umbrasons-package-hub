using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PackageCreator
{
    private static string packageName;
    private static FileInfo zipFile => new("Packages/com.umbrason.package-hub/unity-package-template.zip");

    public static string CreateNewPackage(out string name)
    {
        var localPackagePath = EditorUtility.SaveFolderPanel("Package Directory", "", "New Package");
        packageName = Path.GetFileName(localPackagePath);
        name = packageName.ToLower();
        if (!IsDirectoryEmpty(localPackagePath))
        {
            Debug.LogError("Directory must be empty!");
            return null;
        }
        System.IO.Compression.ZipFile.ExtractToDirectory(zipFile.FullName, localPackagePath);
        ReplaceCrawl(new(localPackagePath));
        return localPackagePath;
    }

    private static void ReplaceCrawl(DirectoryInfo dir)
    {
        foreach (var file in dir.EnumerateFiles()) ReplaceInFile(file);
        foreach (var subdir in dir.EnumerateDirectories()) ReplaceCrawl(subdir);
    }

    private static void ReplaceInFile(FileInfo file)
    {
        var newFilePath = Path.Combine(file.Directory.FullName, ReplaceStringVariables(file.Name));
        if (!file.FullName.Equals(newFilePath))
            file.MoveTo(newFilePath);

        var text = File.ReadAllText(file.FullName);
        text = ReplaceStringVariables(text);
        File.WriteAllText(file.FullName, text);
    }

    private static string ReplaceStringVariables(string text)
    {
        text = text.Replace("$UniqueGuid", Guid.NewGuid().ToString("N"));
        text = text.Replace("$uniqueguid", Guid.NewGuid().ToString("N"));
        text = text.Replace("$PackageName", packageName);
        text = text.Replace("$packagename", packageName.ToLower().Replace(" ", ""));
        return text;
    }


    private static bool IsDirectoryEmpty(string path)
    {
        if (!Directory.Exists(path)) return true;
        using (IEnumerator<string> en = Directory.EnumerateFileSystemEntries(path).GetEnumerator())
        {
            return !en.MoveNext();
        }
    }
}
