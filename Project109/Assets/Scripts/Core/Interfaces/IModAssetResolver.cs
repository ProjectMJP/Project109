using System.IO;

public interface IModAssetResolver
{
    /// <summary>
    /// 모드 디렉토리 경로를 받아, 파일 경로 문자열을 실제 리소스(이미지, 스크립트 등)와 링크하고 무결성을 검증합니다.
    /// </summary>
    /// <param name="modDirectory">현재 파싱중인 모드의 루트 폴더 절대 경로</param>
    /// <returns>모든 에셋이 성공적으로 링크되고 검증되었는지 여부</returns>
    bool ResolveAndValidate(string modDirectory);
}
