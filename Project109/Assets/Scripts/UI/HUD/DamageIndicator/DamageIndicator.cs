using System.Collections;
using TMPro;
using UnityEngine;

public class DamageIndicator : MonoBehaviour
{
    [SerializeField] private TextMeshPro _textMesh;
    
    // 연출 파라미터
    private Vector3 _velocity;
    private float _gravity = 9.8f;
    private float _duration = 1.0f;
    private Color _originalColor;
    
    private System.Action<DamageIndicator> _onComplete;

    public void Setup(string text, Color color, float scaleMultiplier, Vector3 startPos, System.Action<DamageIndicator> onComplete)
    {
        transform.position = startPos;
        transform.localScale = Vector3.one * scaleMultiplier;
        
        if (_textMesh != null)
        {
            _textMesh.text = text;
            _textMesh.color = color;
        }
        _originalColor = color;
        
        _onComplete = onComplete;

        // 좌우 무작위 각도와 위 방향으로 튀기는 포물선 초기 속도 부여
        float angle = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
        float speed = Random.Range(3.5f, 5.0f);
        _velocity = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0) * speed;

        // 카메라 방향을 바라보도록 설정 (Ortho 뷰 최적화: 시선 반전)
        if (Camera.main != null)
        {
            transform.forward = -Camera.main.transform.forward;
        }

        StartCoroutine(AnimateRoutine());
    }

    private IEnumerator AnimateRoutine()
    {
        float elapsed = 0f;

        // 초기 튀어오르는 "크리티컬 펀치" 연출 (크기가 살짝 순간 확대됐다가 제자리로)
        Vector3 baseScale = transform.localScale;
        transform.localScale = baseScale * 1.3f;
        
        while (elapsed < _duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            // 1. 물리 궤적 연산 (중력 반영)
            _velocity.y -= _gravity * dt;
            transform.position += _velocity * dt;

            // 2. 부드러운 축소 복원
            if (elapsed < 0.15f)
            {
                transform.localScale = Vector3.Lerp(baseScale * 1.3f, baseScale, elapsed / 0.15f);
            }

            // 3. 후반부 알파 페이드아웃
            if (elapsed > _duration * 0.5f && _textMesh != null)
            {
                float t = (elapsed - _duration * 0.5f) / (_duration * 0.5f);
                Color c = _originalColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                _textMesh.color = c;
            }

            yield return null;
        }

        // 반환 콜백 호출
        _onComplete?.Invoke(this);
    }
}
