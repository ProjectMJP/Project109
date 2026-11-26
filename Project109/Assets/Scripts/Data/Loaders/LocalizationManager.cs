using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// 다국어 번역 테이블을 관리하고 기본값(Fallback) 우회를 제공하는 정적 매니저입니다.
/// 모드 폴더 내 YAML/Localization/{lang}.yaml 또는 Localization/{lang}.yaml 파일을 로드합니다.
/// 번역 키가 존재하지 않을 경우 YAML 원본에 작성된 기본 텍스트로 자동 폴백(Fallback)합니다.
/// </summary>
public static class LocalizationManager
{
    public const string DEFAULT_LANGUAGE = "ko";

    private static string _currentLanguage = DEFAULT_LANGUAGE;
    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? DEFAULT_LANGUAGE : value.Trim().ToLowerInvariant();
            if (_currentLanguage != normalized)
            {
                _currentLanguage = normalized;
                OnLanguageChanged?.Invoke(_currentLanguage);
            }
        }
    }

    public static event Action<string> OnLanguageChanged;

    // 언어 코드(예: "ko", "en", "ja") -> (번역 키 -> 번역 문자열)
    private static readonly Dictionary<string, Dictionary<string, string>> _languageTables =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    private static readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _currentLanguage = DEFAULT_LANGUAGE;
        _languageTables.Clear();
        OnLanguageChanged = null;
    }

    /// <summary>
    /// 모든 번역 테이블을 초기화합니다. (ModLoader.LoadAllMods 시작 시 호출)
    /// </summary>
    public static void Clear()
    {
        _languageTables.Clear();
    }

    /// <summary>
    /// 지정된 모드 루트 디렉터리에서 다국어 YAML 파일들을 탐색하여 병합 로드합니다.
    /// 지원 경로: {modDir}/YAML/Localization/*.yaml 및 {modDir}/Localization/*.yaml
    /// </summary>
    public static void LoadFromModDirectory(string modDir)
    {
        if (string.IsNullOrEmpty(modDir) || !Directory.Exists(modDir))
        {
            return;
        }

        LoadDirectoryIfExists(Path.Combine(modDir, "YAML", "Localization"));
        LoadDirectoryIfExists(Path.Combine(modDir, "Localization"));
    }

    private static void LoadDirectoryIfExists(string localizationDir)
    {
        if (!Directory.Exists(localizationDir))
        {
            return;
        }

        string[] yamlFiles = Directory.GetFiles(localizationDir, "*.yaml", SearchOption.AllDirectories);
        foreach (string file in yamlFiles)
        {
            string langCode = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            if (string.IsNullOrEmpty(langCode))
            {
                continue;
            }

            try
            {
                string yamlContent = File.ReadAllText(file);
                var parsed = _yamlDeserializer.Deserialize<Dictionary<string, string>>(yamlContent);
                if (parsed == null)
                {
                    continue;
                }

                if (!_languageTables.TryGetValue(langCode, out var table))
                {
                    table = new Dictionary<string, string>(StringComparer.Ordinal);
                    _languageTables[langCode] = table;
                }

                foreach (var kvp in parsed)
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                    {
                        table[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocalizationManager] 다국어 YAML 파싱 실패 ({file}): {e.Message}");
            }
        }
    }

    /// <summary>
    /// 현재 설정된 언어(또는 기본 폴백 언어)에서 번역 텍스트 조회를 시도합니다.
    /// </summary>
    public static bool TryGet(string key, out string localizedText)
    {
        localizedText = null;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        // 1. 현재 선택된 언어 테이블 조회
        if (_languageTables.TryGetValue(_currentLanguage, out var currentTable) &&
            currentTable.TryGetValue(key, out localizedText) &&
            !string.IsNullOrEmpty(localizedText))
        {
            return true;
        }

        // 2. 기본 언어(DEFAULT_LANGUAGE) 폴백 조회
        if (!string.Equals(_currentLanguage, DEFAULT_LANGUAGE, StringComparison.OrdinalIgnoreCase) &&
            _languageTables.TryGetValue(DEFAULT_LANGUAGE, out var defaultTable) &&
            defaultTable.TryGetValue(key, out localizedText) &&
            !string.IsNullOrEmpty(localizedText))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 번역 키가 존재하면 번역된 텍스트를 반환하고, 없으면 YAML 데이터에 정의된 기본 텍스트(defaultText)로 우회합니다.
    /// </summary>
    public static string GetOrDefault(string key, string defaultText)
    {
        if (TryGet(key, out string localized))
        {
            return localized;
        }
        return defaultText ?? string.Empty;
    }
}

