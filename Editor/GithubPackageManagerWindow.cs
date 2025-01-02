using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Octokit;
using System.Runtime.Serialization.Formatters.Binary;
using System;
using System.IO;
using System.Text.RegularExpressions;

public class GithubPackageManagerWindow : EditorWindow
{
    private static Texture2D m_icon;
    public static Texture2D Icon { get { return m_icon ??= AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Umbrasons-package-hub/Editor/Icons/GithubPackageBrowser.png"); } }

    [MenuItem("Window/Github package browser")]
    static void CreateOrShow()
    {
        // Get existing open window or if none, make a new one:
        GithubPackageManagerWindow window = (GithubPackageManagerWindow)EditorWindow.GetWindow(typeof(GithubPackageManagerWindow));
        window.Show();
    }

    private const float LIST_WIDTH = 300f;
    private GitHubClient githubClient;
    private Dictionary<string, RepositoryData[]> repos = new Dictionary<string, RepositoryData[]>();
    private Dictionary<string, bool> reposExpanded = new Dictionary<string, bool>();
    private (string, int) selected;
    private List<string> installedRepos;


    private string lastUpdated = "a long time ago";

    void OnEnable()
    {
        Initialize();
    }

    void Initialize()
    {
        //fetchRepos("Umbrason");
        titleContent = new GUIContent("Github Package Browser", Icon, "Umbrason's Github Package Browser");
        lastUpdated = EditorPrefs.GetString($"{this.GetType().Name}_LastUpdate");
        var keyString = EditorPrefs.GetString($"{this.GetType().Name}_RepoKeys", "not found");
        var valueString = EditorPrefs.GetString($"{this.GetType().Name}_RepoValues", "not found");
        var keys = Deserialize<string[]>(keyString);
        var values = Deserialize<RepositoryData[][]>(valueString);
        if (keys == null || values == null)
            return;
        for (int i = 0; i < keys.Length; i++)
        {
            repos.Add(keys[i], values[i]);
            reposExpanded.Add(keys[i], true);
        }
    }

    public void OnDisable()
    {
        EditorPrefs.SetString($"{this.GetType().Name}_LastUpdate", lastUpdated);
        var keyString = Serialize(repos.Keys.ToArray());
        var valueString = Serialize(repos.Values.ToArray());
        if (valueString == null || keyString == null)
            return;
        EditorPrefs.SetString($"{this.GetType().Name}_RepoKeys", keyString);
        EditorPrefs.SetString($"{this.GetType().Name}_RepoValues", valueString);
    }

    string Serialize(object obj)
    {
        using (var stream = new MemoryStream())
        {
            var bf = new BinaryFormatter();
            bf.Serialize(stream, obj);
            var bytes = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(bytes, 0, bytes.Length);
            var str = new string(bytes.Select((b) => (char)(b + 1)).ToArray());
            return str;
        }
    }

    T Deserialize<T>(string s)
    {
        if (s.Length == 0)
            return default(T);
        var bytes = s.Select((c) => (byte)(c - 1)).ToArray();
        using (var stream = new MemoryStream(bytes, 0, bytes.Length))
        {
            stream.Position = 0;
            var bf = new BinaryFormatter();
            var obj = bf.Deserialize(stream);
            return (T)obj;
        }
    }

    async void fetchRepos(string user)
    {
        var dateTime = System.DateTime.Now;
        lastUpdated = dateTime.ToString("m") + ", " + dateTime.ToString("t");
        githubClient = new GitHubClient(new ProductHeaderValue("Unity3D-Github-Package-Browser"));
        repos[user] = (await githubClient.Repository.GetAllForUser("Umbrason")).Select((x) => new RepositoryData(x)).ToArray();
        reposExpanded[user] = true;
    }

    void OnGUI()
    {
        var width = this.position.width;

        GUILayout.BeginArea(new Rect(Vector2.down * 4, position.size + Vector2.up * 8));
        EditorGUILayout.BeginHorizontal();
        //List
        EditorGUILayout.BeginVertical(GUILayout.Width(LIST_WIDTH));
        GUILayout.Space(4);
        DrawRepositoryList();
        ColoredBox("", Color.black, GUILayout.ExpandHeight(true), GUILayout.Width(1f));
        GUILayout.FlexibleSpace();
        DrawRefreshArea();
        GUILayout.Space(4);
        EditorGUILayout.EndVertical();

        ColoredBox("", Color.black, GUILayout.ExpandHeight(true), GUILayout.Width(1f));

        //Description
        EditorGUILayout.BeginVertical(GUILayout.Width(width - LIST_WIDTH - 1));
        GUILayout.Space(4);
        DrawDescription();
        DrawInstallButton();
        GUILayout.Space(4);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawDescription()
    {
        ColoredBox("", Color.black, GUILayout.ExpandWidth(true));
        if (!(selected.Item1 != null && repos.ContainsKey(selected.Item1) && (repos[selected.Item1].Count() > selected.Item2)))
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("no package selected", style, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            return;
        }
        var repository = repos[selected.Item1][selected.Item2];
        var titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 24;
        titleStyle.fontStyle = FontStyle.Bold;
        GUILayout.Label(repository.name, titleStyle);
        EditorGUILayout.Separator();
        GUILayout.BeginHorizontal();
        GUILayout.Label("source:");
        GUILinkLayout(repository.cloneURL);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"created: {repository.creationDate.ToString("m")}, {repository.creationDate.ToString("t")}");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        EditorGUILayout.Separator();
        GUILayout.Label(repository.description);
        GUILayout.FlexibleSpace();
    }

    void DrawInstallButton()
    {
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("install"))
        {
            TryInstallPackage(repos[selected.Item1][selected.Item2]);
        }
        GUILayout.Space(4f);
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
    }

    void DrawRepositoryList()
    {
        ColoredBox("Github Packages", Color.black, GUILayout.ExpandWidth(true));
        foreach (var user in repos.Keys)
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.8f, .8f, .8f);
            reposExpanded[user] = EditorGUILayout.BeginFoldoutHeaderGroup(reposExpanded[user], user);
            GUI.backgroundColor = backgroundColor;
            if (!reposExpanded[user])
                continue;
            for (int i = 0; i < repos[user].Count(); i++)
            {
                var repo = repos[user][i];
                var name = repo.name;
                var isSelected = !(user.Equals(selected.Item1) && i == selected.Item2);
                if (DrawRepoListButton(name, isSelected))
                    selected = (user, i);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        if (DrawRepoListButton("Create New", false))
        {
            var path = PackageCreator.CreateNewPackage(out string name);
            var request = UnityEditor.PackageManager.Client.Add($"com.umbrason.{name}@file:{path}");
            while (InstallOperation(request).MoveNext())
                continue;
        }
    }

    bool DrawRepoListButton(string name, bool selected)
    {
        var backgroundColor = GUI.backgroundColor;
        EditorGUILayout.BeginHorizontal();
        var anchor = GUI.skin.button.alignment;
        var guiStyle = GUI.skin.button;
        guiStyle.alignment = TextAnchor.MiddleLeft;
        if (selected)
            GUI.backgroundColor = new Color(.8f, .8f, .8f);
        var buttonText = name.Length > 43 ? $"{name.Substring(0, Mathf.Min(40, name.Length))}..." : name;
        var result = GUILayout.Button(buttonText);
        guiStyle.alignment = anchor;
        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = backgroundColor;
        return result;
    }

    void DrawRefreshArea()
    {
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        var textStyle = new GUIStyle(GUI.skin.label);
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.richText = true;
        GUILayout.Label($"<i>Last update {lastUpdated}</i>", textStyle);
        if (GUILayout.Button("refresh"))
            fetchRepos("Umbrason");
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
    }

    void ColoredBox(string text, Color color, params GUILayoutOption[] options)
    {
        var oldColor = GUI.backgroundColor;
        GUI.backgroundColor = color;
        var style = new GUIStyle(GUI.skin.box);
        style.margin = new RectOffset();
        GUILayout.Box(text, style, options);
        GUI.backgroundColor = oldColor;
    }
    private void GUILinkLayout(string URL) => GUILinkLayout(URL, URL);
    private void GUILinkLayout(string text, string URL)
    {
        GUILayout.BeginHorizontal();
        var linkSkin = new GUIStyle(GUI.skin.label);
        linkSkin.richText = true;
        GUI.color = new Color(0x4c / 255f, 0x7e / 255f, 0xff / 255f);
        if (GUILayout.Button($"<b>{text}</b>", linkSkin))
            UnityEngine.Application.OpenURL(URL);
        var lastRect = GUILayoutUtility.GetLastRect();
        lastRect.y += 2;
        var underlineText = new Regex("\\S").Replace(text, "_");
        underlineText += underlineText;
        GUI.Label(lastRect, underlineText, linkSkin);
        GUI.color = Color.white;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    void TryInstallPackage(RepositoryData repository)
    {
        //        if (UnityEditor.PackageManager.Client.List().Result.Where((x) => x.name.ToLower().Contains(repository.FullName.ToLower())).Count() != 0)
        //          return;
        var request = UnityEditor.PackageManager.Client.Add($"{repository.PackageName}@{repository.cloneURL}");
        while (InstallOperation(request).MoveNext())
            continue;
    }

    IEnumerator InstallOperation(UnityEditor.PackageManager.Requests.AddRequest addRequest)
    {
        while (!addRequest.IsCompleted)
            yield return null;
        if (addRequest.Error != null)
            Debug.LogError(addRequest.Error.message);
    }


    [Serializable]
    private struct RepositoryData
    {
        public RepositoryData(Repository repository)
        {
            this.name = repository.Name;
            this.owner = repository.Owner.Login;
            this.cloneURL = repository.CloneUrl;
            this.creationDate = repository.CreatedAt;
            this.description = repository.Description;
            Debug.Log(repository);
        }
        public string name, owner, cloneURL, description;
        public DateTimeOffset creationDate;
        public string FullName { get { return owner + "\\" + name; } }
        public string PackageName { get { return $"com.{owner.ToLower()}.{name.ToLower()}"; } }

    }

}
