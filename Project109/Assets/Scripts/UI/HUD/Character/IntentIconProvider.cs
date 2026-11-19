using UnityEngine;

/// <summary>
/// 적 행동 예고 분류(EnemyIntentType)에 대응하는 색상, 한글 라벨, 아이콘 스프라이트를 제공하는 UI 공용 유틸리티입니다.
/// 외부 텍스처가 Addressables에 없을 경우 정적 캐싱된 32x32 심볼 스프라이트를 반환하여 런타임 GC 할당을 0으로 유지합니다.
/// </summary>
public static class IntentIconProvider
{
    private const int IconSize = 32;
    private static readonly Sprite[] _fallbackSprites = new Sprite[9];

    /// <summary>
    /// 행동 예고 유형별 UI 배지 대표 색상을 반환합니다.
    /// </summary>
    public static Color GetIntentColor(EnemyIntentType type)
    {
        switch (type)
        {
            case EnemyIntentType.Attack:
                return new Color(0.92f, 0.24f, 0.24f, 1f);
            case EnemyIntentType.AttackMove:
                return new Color(0.98f, 0.45f, 0.16f, 1f);
            case EnemyIntentType.Defend:
                return new Color(0.24f, 0.62f, 0.96f, 1f);
            case EnemyIntentType.Buff:
                return new Color(0.24f, 0.82f, 0.44f, 1f);
            case EnemyIntentType.Debuff:
                return new Color(0.70f, 0.34f, 0.92f, 1f);
            case EnemyIntentType.MoveOnly:
                return new Color(0.92f, 0.74f, 0.20f, 1f);
            case EnemyIntentType.Stunned:
                return new Color(0.54f, 0.58f, 0.64f, 1f);
            case EnemyIntentType.Special:
                return new Color(0.96f, 0.76f, 0.26f, 1f);
            default:
                return new Color(0.45f, 0.48f, 0.54f, 0.8f);
        }
    }

    /// <summary>
    /// 행동 예고 유형별 한글 라벨을 반환합니다.
    /// </summary>
    public static string GetIntentLabel(EnemyIntentType type)
    {
        switch (type)
        {
            case EnemyIntentType.Attack: return "공격";
            case EnemyIntentType.AttackMove: return "접근 공격";
            case EnemyIntentType.Defend: return "방어";
            case EnemyIntentType.Buff: return "강화";
            case EnemyIntentType.Debuff: return "약화";
            case EnemyIntentType.MoveOnly: return "이동";
            case EnemyIntentType.Stunned: return "행동 불가";
            case EnemyIntentType.Special: return "특수 행동";
            default: return "대기";
        }
    }

    /// <summary>
    /// 행동 예고 유형에 맞는 아이콘 스프라이트를 반환합니다.
    /// Addressables("Intent_&lt;Type&gt;") 조회를 우선하고, 없으면 정적 캐싱된 32x32 심볼을 반환합니다.
    /// </summary>
    public static Sprite GetIntentIcon(EnemyIntentType type)
    {
        if (AssetCacheManager.instance != null &&
            AssetCacheManager.instance.TryGetTexture(GetTextureKey(type), out Sprite cachedExternal) &&
            cachedExternal != null)
        {
            return cachedExternal;
        }

        int index = (int)type;
        if (index < 0 || index >= _fallbackSprites.Length)
        {
            index = 0;
            type = EnemyIntentType.None;
        }

        if (_fallbackSprites[index] != null)
        {
            return _fallbackSprites[index];
        }

        Sprite generated = CreateFallbackSprite(type);
        _fallbackSprites[index] = generated;
        return generated;
    }

    private static string GetTextureKey(EnemyIntentType type)
    {
        switch (type)
        {
            case EnemyIntentType.Attack: return "Intent_Attack";
            case EnemyIntentType.AttackMove: return "Intent_AttackMove";
            case EnemyIntentType.Defend: return "Intent_Defend";
            case EnemyIntentType.Buff: return "Intent_Buff";
            case EnemyIntentType.Debuff: return "Intent_Debuff";
            case EnemyIntentType.MoveOnly: return "Intent_MoveOnly";
            case EnemyIntentType.Stunned: return "Intent_Stunned";
            case EnemyIntentType.Special: return "Intent_Special";
            default: return "Intent_None";
        }
    }

    private static Sprite CreateFallbackSprite(EnemyIntentType type)
    {
        bool[] mask = new bool[IconSize * IconSize];
        BuildSymbolMask(mask, type);

        Color[] pixels = new Color[IconSize * IconSize];
        Color outlineColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);

        for (int y = 0; y < IconSize; y++)
        {
            for (int x = 0; x < IconSize; x++)
            {
                int idx = y * IconSize + x;
                if (mask[idx])
                {
                    pixels[idx] = Color.white;
                }
                else if (HasNeighborMask(mask, x, y))
                {
                    pixels[idx] = outlineColor;
                }
                else
                {
                    pixels[idx] = Color.clear;
                }
            }
        }

        Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = GetTextureKey(type)
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, IconSize, IconSize), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        sprite.name = GetTextureKey(type);
        return sprite;
    }

    private static void BuildSymbolMask(bool[] mask, EnemyIntentType type)
    {
        switch (type)
        {
            case EnemyIntentType.Attack:
                // 대각선 검날 + 코등이 + 손잡이
                DrawCapsule(mask, 9f, 9f, 25f, 25f, 2.0f);
                DrawCapsule(mask, 7f, 15f, 15f, 7f, 1.6f);
                DrawCapsule(mask, 5f, 5f, 10f, 10f, 1.7f);
                break;

            case EnemyIntentType.AttackMove:
                // 좌측 검 + 우측 전진 화살표
                DrawCapsule(mask, 5f, 7f, 18f, 24f, 1.8f);
                DrawCapsule(mask, 4f, 13f, 12f, 6f, 1.4f);
                DrawCapsule(mask, 20f, 21f, 26f, 15f, 1.6f);
                DrawCapsule(mask, 26f, 15f, 20f, 9f, 1.6f);
                break;

            case EnemyIntentType.Defend:
                // 방패 실루엣
                for (int y = 5; y <= 26; y++)
                {
                    float halfWidth;
                    if (y >= 14)
                    {
                        halfWidth = 8.5f;
                    }
                    else
                    {
                        float t = (y - 5f) / 9f;
                        halfWidth = Mathf.Lerp(1.0f, 8.5f, t);
                    }

                    for (int x = 0; x < IconSize; x++)
                    {
                        if (Mathf.Abs(x - 15.5f) <= halfWidth)
                        {
                            mask[y * IconSize + x] = true;
                        }
                    }
                }
                break;

            case EnemyIntentType.Buff:
                // 이중 상향 셰브론
                DrawCapsule(mask, 8f, 16f, 15.5f, 24f, 1.9f);
                DrawCapsule(mask, 15.5f, 24f, 23f, 16f, 1.9f);
                DrawCapsule(mask, 8f, 8f, 15.5f, 16f, 1.9f);
                DrawCapsule(mask, 15.5f, 16f, 23f, 8f, 1.9f);
                break;

            case EnemyIntentType.Debuff:
                // 이중 하향 셰브론
                DrawCapsule(mask, 8f, 23f, 15.5f, 15f, 1.9f);
                DrawCapsule(mask, 15.5f, 15f, 23f, 23f, 1.9f);
                DrawCapsule(mask, 8f, 15f, 15.5f, 7f, 1.9f);
                DrawCapsule(mask, 15.5f, 7f, 23f, 15f, 1.9f);
                break;

            case EnemyIntentType.MoveOnly:
                // 우향 이동 화살표
                DrawCapsule(mask, 6f, 15.5f, 24f, 15.5f, 2.2f);
                DrawCapsule(mask, 17f, 23f, 25f, 15.5f, 2.0f);
                DrawCapsule(mask, 25f, 15.5f, 17f, 8f, 2.0f);
                break;

            case EnemyIntentType.Stunned:
                // 행동 불가 X 심볼
                DrawCapsule(mask, 8f, 8f, 23f, 23f, 2.2f);
                DrawCapsule(mask, 8f, 23f, 23f, 8f, 2.2f);
                break;

            case EnemyIntentType.Special:
                // 4방향 별빛/다이아몬드 심볼
                for (int y = 0; y < IconSize; y++)
                {
                    for (int x = 0; x < IconSize; x++)
                    {
                        float dx = Mathf.Abs(x - 15.5f);
                        float dy = Mathf.Abs(y - 15.5f);
                        bool inCross = (dx <= 1.8f && dy <= 10.5f) || (dy <= 1.8f && dx <= 10.5f);
                        bool inDiamond = (dx + dy) <= 6.2f;
                        if (inCross || inDiamond)
                        {
                            mask[y * IconSize + x] = true;
                        }
                    }
                }
                break;

            default:
                FillCircle(mask, 15.5f, 15.5f, 3.2f);
                break;
        }
    }

    private static void DrawCapsule(bool[] mask, float x0, float y0, float x1, float y1, float radius)
    {
        float vx = x1 - x0;
        float vy = y1 - y0;
        float lenSq = vx * vx + vy * vy;
        float rSq = radius * radius;

        for (int y = 0; y < IconSize; y++)
        {
            for (int x = 0; x < IconSize; x++)
            {
                float wx = x - x0;
                float wy = y - y0;
                float t = lenSq > 0.0001f ? Mathf.Clamp01((wx * vx + wy * vy) / lenSq) : 0f;
                float px = x0 + t * vx;
                float py = y0 + t * vy;
                float dx = x - px;
                float dy = y - py;
                if (dx * dx + dy * dy <= rSq)
                {
                    mask[y * IconSize + x] = true;
                }
            }
        }
    }

    private static void FillCircle(bool[] mask, float cx, float cy, float radius)
    {
        float rSq = radius * radius;
        for (int y = 0; y < IconSize; y++)
        {
            for (int x = 0; x < IconSize; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                if (dx * dx + dy * dy <= rSq)
                {
                    mask[y * IconSize + x] = true;
                }
            }
        }
    }

    private static bool HasNeighborMask(bool[] mask, int x, int y)
    {
        for (int oy = -1; oy <= 1; oy++)
        {
            int ny = y + oy;
            if (ny < 0 || ny >= IconSize) continue;

            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox;
                if (nx < 0 || nx >= IconSize) continue;

                if (mask[ny * IconSize + nx])
                {
                    return true;
                }
            }
        }
        return false;
    }
}
