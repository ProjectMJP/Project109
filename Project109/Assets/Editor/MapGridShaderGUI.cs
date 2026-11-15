using UnityEditor;
using UnityEngine;

public class MapGridShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // 1. 기본 프로퍼티들을 화면에 그립니다.
        materialEditor.PropertiesDefaultGUI(properties);

        // 2. 머티리얼 대상을 가져와 _QueueOffset 값에 따라 실제 renderQueue를 갱신합니다.
        Material material = materialEditor.target as Material;
        if (material != null)
        {
            MaterialProperty queueOffsetProp = FindProperty("_QueueOffset", properties, false);
            if (queueOffsetProp != null)
            {
                int baseQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 3000
                int offset = Mathf.RoundToInt(queueOffsetProp.floatValue);

                // 실제 머티리얼의 렌더링 큐를 직접 업데이트
                material.renderQueue = baseQueue + offset;
            }
        }
    }
}
