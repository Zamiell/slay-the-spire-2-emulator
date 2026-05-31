using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using Steamworks;

namespace MegaCrit.Sts2.Core.Modding;

public static class ModManager
{
    public delegate void MetricsUploadHook(
        SerializableRun run,
        bool isVictory,
        ulong localPlayerId
    );

    private static bool _allowInitForTests;

    private static List<Mod> _mods = new List<Mod>();

    private static bool _initialized;

    private static Callback<ItemInstalled_t>? _steamItemInstalledCallback;

    private static ModSettings? _settings;

    private static IModManagerFileIo? _fileIo;

    private static SemanticVersion? _gameVersion;

    private static readonly Dictionary<string, string> _circularDependencies =
        new Dictionary<string, string>();

    private static bool? _hasHarmonyPatches;

    public static IReadOnlyList<Mod> Mods => _mods;

    public static bool PlayerAgreedToModLoading => _settings?.PlayerAgreedToModLoading ?? false;

    public static event Action<Mod>? OnModDetected;

    public static event MetricsUploadHook? OnMetricsUpload;

    public static void Initialize(
        IModManagerFileIo fileIo,
        ModSettings? settings,
        SemanticVersion? gameVersion
    )
    {
        _settings = settings;
        _fileIo = fileIo;
        _gameVersion = gameVersion;
        if (_gameVersion == null)
        {
            Log.Warn(
                "Game doesn't have ReleaseInfo. We can't check version compatibility, so assuming all mods are supported."
            );
        }
        if (CommandLineHelper.HasArg("nomods"))
        {
            Log.Info("'nomods' passed as executable argument, skipping mod initialization");
        }
        else
        {
            if (TestMode.IsOn && !_allowInitForTests)
            {
                return;
            }
            _allowInitForTests = false;
            AppDomain.CurrentDomain.AssemblyResolve += HandleAssemblyResolveFailure;
            string executablePath = OS.GetExecutablePath();
            string directoryName = Path.GetDirectoryName(executablePath);
            string path = Path.Combine(directoryName, "mods");
            if (fileIo.DirectoryExists(path))
            {
                ReadModsInDirRecursive(path, ModSource.ModsDirectory, null);
            }
            if (SteamInitializer.Initialized)
            {
                ReadSteamMods();
            }
            if (_mods.Count == 0)
            {
                return;
            }
            SortModList(_settings?.ModList ?? new List<SettingsSaveMod>());
            foreach (Mod mod2 in _mods)
            {
                TryLoadMod(mod2);
            }
            if (IsRunningModded())
            {
                int value = _mods.Count((Mod m) => m.state == ModLoadState.Loaded);
                Log.Info($" --- RUNNING MODDED! --- Loaded {value} mods ({_mods.Count} total)");
            }
            _initialized = true;
            if (_settings == null)
            {
                return;
            }
            List<SettingsSaveMod> list = new List<SettingsSaveMod>();
            foreach (Mod mod in _mods)
            {
                SettingsSaveMod settingsSaveMod = new SettingsSaveMod(mod);
                bool isEnabled =
                    _settings
                        .ModList.FirstOrDefault((SettingsSaveMod m) => m.Id == mod.manifest?.id)
                        ?.IsEnabled
                    ?? true;
                settingsSaveMod.IsEnabled = isEnabled;
                list.Add(settingsSaveMod);
            }
            _settings.ModList = list;
        }
    }

    public static void ResetForTests()
    {
        if (TestMode.IsOff)
        {
            throw new NotImplementedException(
                "Tried to reset ModManager outside of tests! This is not allowed, as we cannot unload DLLs or PCKs"
            );
        }
        _mods.Clear();
        _initialized = false;
        _settings = null;
        _fileIo = null;
        _allowInitForTests = true;
        _circularDependencies.Clear();
    }

    private static void SortModList(List<SettingsSaveMod> manualOrdering)
    {
        List<int> list = new List<int>();
        Dictionary<Mod, List<Mod>> dictionary = new Dictionary<Mod, List<Mod>>();
        for (int i = 0; i < _mods.Count; i++)
        {
            Mod mod = _mods[i];
            int num = 0;
            if (mod.manifest?.dependencies != null)
            {
                foreach (ModDependency declaredDependency in mod.manifest.dependencies)
                {
                    Mod mod2 = _mods.FirstOrDefault(
                        (Mod m) => m.manifest?.id == declaredDependency.id
                    );
                    if (mod2 != null)
                    {
                        num++;
                        if (!dictionary.TryGetValue(mod2, out var value))
                        {
                            value = (dictionary[mod2] = new List<Mod>());
                        }
                        value.Add(mod);
                    }
                }
            }
            list.Add(num);
        }
        PriorityQueue<Mod, int> priorityQueue = new PriorityQueue<Mod, int>();
        Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
        for (int num2 = 0; num2 < manualOrdering.Count; num2++)
        {
            dictionary2[manualOrdering[num2].Id] = num2;
        }
        for (int num3 = 0; num3 < _mods.Count; num3++)
        {
            if (list[num3] == 0)
            {
                int value2;
                int priority = (
                    dictionary2.TryGetValue(_mods[num3].manifest.id, out value2)
                        ? value2
                        : 999999999
                );
                priorityQueue.Enqueue(_mods[num3], priority);
            }
        }
        List<Mod> list3 = new List<Mod>();
        while (priorityQueue.Count > 0)
        {
            Mod mod3 = priorityQueue.Dequeue();
            list3.Add(mod3);
            if (!dictionary.TryGetValue(mod3, out var value3))
            {
                continue;
            }
            foreach (Mod item in value3)
            {
                int num4 = _mods.IndexOf(item);
                if (num4 < 0)
                {
                    throw new InvalidOperationException("Bug in mod sorting logic!");
                }
                list[num4]--;
                if (list[num4] == 0)
                {
                    int value4;
                    int priority2 = (
                        dictionary2.TryGetValue(item.manifest.id, out value4) ? value4 : 999999999
                    );
                    priorityQueue.Enqueue(item, priority2);
                }
            }
        }
        HashSet<Mod> hashSet = new HashSet<Mod>();
        foreach (Mod item2 in list3)
        {
            hashSet.Add(item2);
        }
        HashSet<Mod> sortedSet = hashSet;
        string value5 = string.Join(
            ", ",
            from m in _mods
            where !sortedSet.Contains(m)
            select m.manifest?.id
        );
        foreach (Mod mod4 in _mods)
        {
            if (!sortedSet.Contains(mod4) && mod4.manifest?.id != null)
            {
                _circularDependencies[mod4.manifest.id] = value5;
            }
        }
        foreach (Mod mod5 in _mods)
        {
            if (!sortedSet.Contains(mod5))
            {
                list3.Add(mod5);
            }
        }
        bool flag = manualOrdering.Count != list3.Count;
        if (!flag)
        {
            for (int num5 = 0; num5 < manualOrdering.Count; num5++)
            {
                if (manualOrdering[num5].Id != list3[num5].manifest?.id)
                {
                    flag = true;
                    break;
                }
            }
        }
        if (flag)
        {
            Log.Info(
                "Mods have been re-sorted because we detected a change or dependency order was broken. New sorting order:"
            );
            for (int num6 = 0; num6 < list3.Count; num6++)
            {
                Log.Info($"  {num6}: {list3[num6].manifest?.name} ({list3[num6].manifest?.id})");
            }
        }
        _mods = list3;
    }

    private static void ReadModsInDirRecursive(string path, ModSource source, List<Mod>? newMods)
    {
        string[] array = _fileIo?.GetFilesAt(path) ?? Array.Empty<string>();
        foreach (string text in array)
        {
            if (text.EndsWith(".json"))
            {
                string text2 = Path.Combine(path, text);
                Log.Info("Found mod manifest file " + text2);
                Mod mod = ReadModManifest(text2, source);
                if (mod != null)
                {
                    _mods.Add(mod);
                    newMods?.Add(mod);
                }
            }
        }
        string[] array2 = _fileIo?.GetDirectoriesAt(path) ?? Array.Empty<string>();
        foreach (string path2 in array2)
        {
            string path3 = Path.Combine(path, path2);
            if (_fileIo.DirectoryExists(path3))
            {
                ReadModsInDirRecursive(path3, source, newMods);
            }
        }
    }

    private static Mod? ReadModManifest(string filename, ModSource source)
    {
        if (_fileIo == null)
        {
            return null;
        }
        try
        {
            using Stream stream = _fileIo.OpenStream(filename, Godot.FileAccess.ModeFlags.Read);
            List<LocString> errors;
            ModManifest modManifest = ModManifest.ReadFromStream(stream, out errors);
            if (modManifest == null)
            {
                throw new InvalidOperationException(
                    "JSON deserialization returned null when trying to deserialize mod manifest!"
                );
            }
            if (modManifest.id == null)
            {
                Log.Error(
                    "Mod manifest "
                        + filename
                        + " is missing the 'id' field! This is not allowed. The mod will not be loaded."
                );
                return null;
            }
            return new Mod
            {
                path = filename.GetBaseDir(),
                modSource = source,
                manifest = modManifest,
                errors = errors,
            };
        }
        catch (Exception ex)
        {
            Log.Error(
                $"Caught {ex.GetType()} trying to deserialize mod manifest json at path {filename}:\n{ex}"
            );
            return null;
        }
    }

    private static void ReadSteamMods()
    {
        uint numSubscribedItems = SteamUGC.GetNumSubscribedItems();
        PublishedFileId_t[] array = new PublishedFileId_t[numSubscribedItems];
        numSubscribedItems = SteamUGC.GetSubscribedItems(array, numSubscribedItems);
        for (int i = 0; i < numSubscribedItems; i++)
        {
            PublishedFileId_t workshopItemId = array[i];
            TryReadModFromSteam(workshopItemId, null);
        }
        _steamItemInstalledCallback = Callback<ItemInstalled_t>.Create(
            OnSteamWorkshopItemInstalled
        );
    }

    private static void TryReadModFromSteam(PublishedFileId_t workshopItemId, List<Mod>? newMods)
    {
        if (
            !SteamUGC.GetItemInstallInfo(
                workshopItemId,
                out var punSizeOnDisk,
                out var pchFolder,
                256u,
                out var punTimeStamp
            )
        )
        {
            Log.Warn(
                $"Could not get Steam Workshop item install info for item {workshopItemId.m_PublishedFileId}"
            );
            return;
        }
        Log.Info(
            $"Looking for mods to load from Steam Workshop mod {workshopItemId.m_PublishedFileId} in {pchFolder} (size {punSizeOnDisk}, last modified {punTimeStamp})"
        );
        if (_fileIo != null && !_fileIo.DirectoryExists(pchFolder))
        {
            Log.Warn("Could not open Steam Workshop folder: " + pchFolder);
        }
        else
        {
            ReadModsInDirRecursive(pchFolder, ModSource.SteamWorkshop, newMods);
        }
    }

    private static void OnSteamWorkshopItemInstalled(ItemInstalled_t ev)
    {
        if ((ulong)ev.m_unAppID.m_AppId != 2868840)
        {
            return;
        }
        Log.Info(
            $"Detected new Steam Workshop item installation, id: {ev.m_nPublishedFileId.m_PublishedFileId}"
        );
        List<Mod> list = new List<Mod>();
        TryReadModFromSteam(ev.m_nPublishedFileId, list);
        foreach (Mod item in list)
        {
            item.state = ModLoadState.AddedAtRuntime;
            ModManager.OnModDetected?.Invoke(item);
        }
    }

    private static void TryLoadMod(Mod mod)
    {
        Assembly assembly = null;
        List<LocString> list = mod.errors ?? new List<LocString>();
        if (mod.manifest == null)
        {
            throw new InvalidOperationException(
                "Tried to load mod before its manifest was loaded!"
            );
        }
        SemanticVersion version;
        if (mod.manifest.version == null)
        {
            Log.Warn("Mod " + mod.manifest.id + " does not declare a version");
        }
        else if (!SemanticVersion.TryFromString(mod.manifest.version, out version))
        {
            Log.Warn(
                $"Mod {mod.manifest.id} declares version {mod.manifest.version} which is not a valid Semantic Version"
            );
        }
        else
        {
            mod.version = version;
        }
        string modId = mod.manifest.id;
        bool flag = _settings?.IsModDisabled(modId, mod.modSource) ?? false;
        bool flag2 = _mods.Any(
            (Mod m) => m.manifest?.id == modId && m.state == ModLoadState.Loaded
        );
        bool flag3 = false;
        bool flag4 = true;
        if (_gameVersion != null)
        {
            SemanticVersion version2;
            if (mod.manifest.minGameVersion == null)
            {
                Log.Warn(
                    "Mod "
                        + mod.manifest.id
                        + " does not declare min game version. Assuming that it is supported."
                );
            }
            else if (!SemanticVersion.TryFromString(mod.manifest.minGameVersion, out version2))
            {
                flag3 = true;
            }
            else
            {
                flag4 = _gameVersion.CompareTo(version2) >= 0;
            }
        }
        string value;
        if (_initialized)
        {
            Log.Info("Skipping loading mod " + modId + ", can't load mods at runtime");
            mod.state = ModLoadState.AddedAtRuntime;
        }
        else if (flag)
        {
            Log.Info("Skipping loading mod " + modId + ", it is set to disabled in settings");
            mod.state = ModLoadState.Disabled;
        }
        else if (!PlayerAgreedToModLoading)
        {
            Log.Info("Skipping loading mod " + modId + ", user has not yet seen the mods warning");
            mod.state = ModLoadState.Disabled;
        }
        else if (flag2)
        {
            LocString locString = new LocString("main_menu_ui", "MOD_ERROR.DUPLICATE_ID");
            locString.Add("id", modId);
            list.Add(locString);
            Log.Error(
                "Tried to load mod with id "
                    + modId
                    + ", but a mod is already loaded with that name!"
            );
            mod.state = ModLoadState.Failed;
        }
        else if (_circularDependencies.TryGetValue(modId, out value))
        {
            LocString locString2 = new LocString("main_menu_ui", "MOD_ERROR.CIRCULAR_DEPENDENCY");
            locString2.Add("id", modId);
            locString2.Add("dependencyChain", value);
            list.Add(locString2);
            Log.Error(
                $"Tried to load mod with id {modId}, but it is part of a circular dependency chain: {value}!"
            );
            mod.state = ModLoadState.Failed;
        }
        else if (flag3)
        {
            LocString locString3 = new LocString("main_menu_ui", "MOD_ERROR.GAME_VERSION_INVALID");
            locString3.Add("id", modId);
            locString3.Add("minGameVersion", mod.manifest.minGameVersion ?? "<null>");
            list.Add(locString3);
            Log.Error(
                $"Mod {mod.manifest.id} declares min game version {mod.manifest.minGameVersion} that can't be parsed! Assuming it is supported"
            );
            mod.state = ModLoadState.Failed;
        }
        else if (!flag4)
        {
            LocString locString4 = new LocString(
                "main_menu_ui",
                "MOD_ERROR.GAME_VERSION_UNSUPPORTED"
            );
            locString4.Add("id", modId);
            locString4.Add("minGameVersion", mod.manifest.minGameVersion ?? "<null>");
            locString4.Add("gameVersion", _gameVersion?.ToString() ?? "<null>");
            list.Add(locString4);
            Log.Error(
                $"Tried to load mod with id {modId}, but its declared min game version {mod.manifest.minGameVersion} is higher than the current game version {_gameVersion}"
            );
            mod.state = ModLoadState.Failed;
        }
        else
        {
            List<string> list2 = new List<string>();
            if (mod.manifest.dependencies != null)
            {
                foreach (ModDependency declaredDependency in mod.manifest.dependencies)
                {
                    Mod mod2 = _mods.FirstOrDefault(
                        (Mod m) => m.manifest?.id == declaredDependency.id
                    );
                    if (mod2 == null || mod2.state != ModLoadState.Loaded)
                    {
                        list2.Add(declaredDependency.id);
                    }
                    else if (declaredDependency.minVersion != null)
                    {
                        if (
                            !SemanticVersion.TryFromString(
                                declaredDependency.minVersion,
                                out SemanticVersion version3
                            )
                        )
                        {
                            LocString locString5 = new LocString(
                                "main_menu_ui",
                                "MOD_ERROR.DEPENDENCY_MIN_VERSION_INVALID"
                            );
                            locString5.Add("id", mod.manifest.id);
                            locString5.Add("dependency", declaredDependency.id);
                            locString5.Add("minVersion", declaredDependency.minVersion);
                            list.Add(locString5);
                            Log.Error(
                                $"Mod {modId} which depends on {declaredDependency.id} with min version {declaredDependency.minVersion} which cannot be parsed"
                            );
                            mod.state = ModLoadState.Failed;
                        }
                        else if (mod2.manifest?.version == null)
                        {
                            LocString locString6 = new LocString(
                                "main_menu_ui",
                                "MOD_ERROR.DEPENDENCY_VERSION_MISSING"
                            );
                            locString6.Add("id", mod.manifest.id);
                            locString6.Add("dependency", declaredDependency.id);
                            locString6.Add("minVersion", declaredDependency.minVersion);
                            list.Add(locString6);
                            Log.Error(
                                $"Tried to load mod {modId} which depends on {declaredDependency.id} with min version {declaredDependency.minVersion}, but the mod declares no version!"
                            );
                            mod.state = ModLoadState.Failed;
                        }
                        else if (mod2.version == null)
                        {
                            LocString locString7 = new LocString(
                                "main_menu_ui",
                                "MOD_ERROR.DEPENDENCY_VERSION_INVALID"
                            );
                            locString7.Add("id", mod.manifest.id);
                            locString7.Add("dependency", declaredDependency.id);
                            locString7.Add("minVersion", declaredDependency.minVersion);
                            locString7.Add("version", mod2.manifest.version);
                            list.Add(locString7);
                            Log.Error(
                                $"Tried to load mod {modId} which depends on {declaredDependency.id} with min version {declaredDependency.minVersion}, but the mod declares version {mod2.manifest.version} which cannot be parsed!"
                            );
                            mod.state = ModLoadState.Failed;
                        }
                        else if (mod2.version.CompareTo(version3) < 0)
                        {
                            LocString locString8 = new LocString(
                                "main_menu_ui",
                                "MOD_ERROR.DEPENDENCY_VERSION_UNSUPPORTED"
                            );
                            locString8.Add("id", mod.manifest.id);
                            locString8.Add("dependency", declaredDependency.id);
                            locString8.Add("minVersion", declaredDependency.minVersion);
                            locString8.Add("version", mod2.manifest.version);
                            list.Add(locString8);
                            Log.Error(
                                $"Tried to load mod {modId} which depends on {declaredDependency.id} with min version {declaredDependency.minVersion} but you have {mod2.version}!"
                            );
                            mod.state = ModLoadState.Failed;
                        }
                    }
                }
                if (list2.Count > 0)
                {
                    string text = string.Join(",", list2);
                    LocString locString9 = new LocString(
                        "main_menu_ui",
                        "MOD_ERROR.MISSING_DEPENDENCY"
                    );
                    locString9.Add("id", mod.manifest.id);
                    locString9.Add("missingCount", list2.Count);
                    locString9.Add("missingDependencies", text);
                    list.Add(locString9);
                    Log.Error(
                        $"Tried to load mod {modId}, but it depends on mods which have not been loaded: {text}!"
                    );
                    mod.state = ModLoadState.Failed;
                }
            }
        }
        if (mod.state != ModLoadState.None)
        {
            mod.errors = ((list.Count == 0) ? null : list);
            ModManager.OnModDetected?.Invoke(mod);
            return;
        }
        try
        {
            bool flag5 = false;
            string text2 = Path.Combine(mod.path, modId + ".dll");
            if (mod.manifest.hasDll)
            {
                if (_fileIo != null && _fileIo.FileExists(text2))
                {
                    Log.Info("Loading assembly DLL " + text2);
                    AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(
                        Assembly.GetExecutingAssembly()
                    );
                    if (loadContext != null)
                    {
                        assembly = loadContext.LoadFromAssemblyPath(text2);
                        flag5 = true;
                    }
                }
                else
                {
                    Log.Error(
                        $"Mod manifest for mod {mod.manifest.id} declares that it should load an assembly, but no assembly at path {text2} was found!"
                    );
                }
            }
            string text3 = Path.Combine(mod.path, modId + ".pck");
            if (mod.manifest.hasPck)
            {
                if (_fileIo != null && _fileIo.FileExists(text3))
                {
                    Log.Info("Loading Godot PCK " + text3);
                    if (!ProjectSettings.LoadResourcePack(text3))
                    {
                        throw new InvalidOperationException(
                            "Godot errored while loading PCK file " + modId + "!"
                        );
                    }
                    flag5 = true;
                }
                else
                {
                    Log.Error(
                        $"Mod manifest for mod {mod.manifest.id} declares that it should load a PCK, but no PCK at path {text3} was found!"
                    );
                }
            }
            if (!flag5)
            {
                Log.Warn(
                    "Neither a DLL nor a PCK was loaded for mod "
                        + mod.manifest.id
                        + ", something seems wrong!"
                );
            }
            bool? flag6 = null;
            if (assembly != null)
            {
                flag6 = true;
                List<Type> list3 = (
                    from t in assembly.GetTypes()
                    where t.GetCustomAttribute<ModInitializerAttribute>() != null
                    select t
                ).ToList();
                if (list3.Count > 0)
                {
                    foreach (Type item in list3)
                    {
                        Log.Info($"Calling initializer method of type {item} for {assembly}");
                        bool flag7 = CallModInitializer(item);
                        flag6 = flag6.Value && flag7;
                    }
                }
                else
                {
                    try
                    {
                        Log.Info(
                            $"No ModInitializerAttribute detected. Calling Harmony.PatchAll for {assembly}"
                        );
                        Harmony harmony = new Harmony(
                            (mod.manifest.author ?? "unknown") + "." + modId
                        );
                        harmony.PatchAll(assembly);
                    }
                    catch (Exception value2)
                    {
                        Log.Error(
                            $"Exception caught while trying to run PatchAll on assembly {assembly}:\n{value2}"
                        );
                        flag6 = false;
                    }
                }
            }
            if (flag6 == false)
            {
                LocString locString10 = new LocString("main_menu_ui", "MOD_ERROR.ASSEMBLY_LOAD");
                locString10.Add("id", mod.manifest.id);
                list.Add(locString10);
            }
            Log.Info($"Finished mod initialization for '{mod.manifest.name}' ({modId}).");
            mod.state = ModLoadState.Loaded;
            mod.assembly = assembly;
            mod.errors = ((list.Count == 0) ? null : list);
            ModManager.OnModDetected?.Invoke(mod);
        }
        catch (Exception ex)
        {
            Log.Error($"Exception thrown while loading mod {modId}: {ex}");
            LocString locString11 = new LocString("main_menu_ui", "MOD_ERROR.EXCEPTION");
            locString11.Add("exceptionType", ex.GetType().ToString());
            locString11.Add("id", mod.manifest.id);
            list.Add(locString11);
            mod.state = ModLoadState.Failed;
            mod.assembly = assembly;
            mod.errors = ((list.Count == 0) ? null : list);
            ModManager.OnModDetected?.Invoke(mod);
        }
    }

    private static bool CallModInitializer(Type initializerType)
    {
        ModInitializerAttribute customAttribute =
            initializerType.GetCustomAttribute<ModInitializerAttribute>();
        MethodInfo method = initializerType.GetMethod(
            customAttribute.initializerMethod,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (method == null)
        {
            method = initializerType.GetMethod(
                customAttribute.initializerMethod,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (method != null)
            {
                Log.Error(
                    $"Tried to call mod initializer {initializerType.Name}.{customAttribute.initializerMethod} but it's not static! Declare it to be static"
                );
            }
            else
            {
                Log.Error(
                    $"Found mod initializer class of type {initializerType}, but it does not contain the method {customAttribute.initializerMethod} declared in the ModInitializerAttribute!"
                );
            }
            return false;
        }
        try
        {
            method.Invoke(null, null);
        }
        catch (Exception value)
        {
            Log.Error(
                $"Exception thrown when calling mod initializer of type {initializerType}: {value}"
            );
            return false;
        }
        return true;
    }

    public static IEnumerable<string> GetModdedLocTables(string language, string file)
    {
        foreach (Mod mod in _mods)
        {
            if (mod.state == ModLoadState.Loaded)
            {
                string text = $"res://{mod.manifest.id}/localization/{language}/{file}";
                if (ResourceLoader.Exists(text))
                {
                    yield return text;
                }
            }
        }
    }

    public static List<string>? GetGameplayRelevantModNameList()
    {
        if (!IsRunningModded())
        {
            return null;
        }
        return (
            from m in GetLoadedMods()
            where m.manifest?.affectsGameplay ?? true
            select m.manifest?.id + "-" + m.manifest?.version
        ).ToList();
    }

    private static Assembly HandleAssemblyResolveFailure(object? source, ResolveEventArgs ev)
    {
        if (ev.Name.StartsWith("sts2,"))
        {
            Log.Info(
                $"Failed to resolve assembly '{ev.Name}' but it looks like the STS2 assembly. Resolving using {Assembly.GetExecutingAssembly()}"
            );
            return Assembly.GetExecutingAssembly();
        }
        if (ev.Name.StartsWith("0Harmony,"))
        {
            Log.Info(
                $"Failed to resolve assembly '{ev.Name}' but it looks like the Harmony assembly. Resolving using {typeof(Harmony).Assembly}"
            );
            return typeof(Harmony).Assembly;
        }
        return null;
    }

    public static void CallMetricsHooks(SerializableRun run, bool isVictory, ulong localPlayerId)
    {
        ModManager.OnMetricsUpload?.Invoke(run, isVictory, localPlayerId);
    }

    public static bool IsRunningModded()
    {
        return _mods.Any(
            delegate(Mod m)
            {
                ModLoadState state = m.state;
                return (uint)(state - 1) <= 1u;
            }
        );
    }

    public static bool HasHarmonyPatches()
    {
        try
        {
            bool valueOrDefault = _hasHarmonyPatches == true;
            if (!_hasHarmonyPatches.HasValue)
            {
                valueOrDefault = Harmony.GetAllPatchedMethods().Any();
                _hasHarmonyPatches = valueOrDefault;
            }
        }
        catch
        {
            _hasHarmonyPatches = true;
        }
        return _hasHarmonyPatches.Value;
    }

    public static IEnumerable<Mod> GetLoadedMods()
    {
        return _mods.Where((Mod m) => m.state == ModLoadState.Loaded);
    }

    public static void Dispose()
    {
        _steamItemInstalledCallback?.Dispose();
    }
}
