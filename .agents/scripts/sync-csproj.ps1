# Unity Assembly-CSharp.csproj 자동 동기화 스크립트
# 외부(IDE / 에이전트)에서 C# 파일을 생성, 이동, 삭제했을 때 발생하는 CS0246 / CS2001 오류를 원클릭으로 해결합니다.

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path (Join-Path $scriptDir "..\..\Project109")
$projPath = Join-Path $projectRoot "Assembly-CSharp.csproj"
$assetsPath = Join-Path $projectRoot "Assets"

if (-not (Test-Path $projPath)) {
    Write-Error "Assembly-CSharp.csproj 파일을 찾을 수 없습니다: $projPath"
    exit 1
}

Write-Host "[sync-csproj] 실제 C# 파일 목록을 검색합니다: $assetsPath"

# 1. Assets 하위의 모든 .cs 파일 수집
# - Editor 폴더 제외 (Assembly-CSharp-Editor.csproj 소관)
# - YamlDotNet 등 자체 asmdef가 있는 서브 패키지 폴더 제외
$targetFiles = Get-ChildItem -Path $assetsPath -Filter *.cs -Recurse | Where-Object {
    $_.FullName -notmatch "\\Editor\\" -and
    $_.FullName -notmatch "\\Editor$" -and
    $_.FullName -notmatch "\\YamlDotNet\\"
} | ForEach-Object {
    $_.FullName.Substring($_.FullName.IndexOf("Assets\"))
} | Sort-Object

Write-Host "[sync-csproj] 대상 C# 파일 개수: $($targetFiles.Count)개"

# 2. csproj 파일 읽기 및 Compile 태그 갱신
$content = Get-Content -Path $projPath -Raw -Encoding UTF8
$compileItems = ($targetFiles | ForEach-Object { "    <Compile Include=`"$_`" />" }) -join "`r`n"
$pattern = '(?s)<ItemGroup>\s*<Compile Include=.*?</ItemGroup>'
$replacement = "<ItemGroup>`r`n$compileItems`r`n  </ItemGroup>"

if ($content -match $pattern) {
    $newContent = $content -replace $pattern, $replacement
    [System.IO.File]::WriteAllText($projPath, $newContent, [System.Text.Encoding]::UTF8)
    Write-Host "[sync-csproj] Assembly-CSharp.csproj 동기화 완료! ($($targetFiles.Count)개 소스 파일 등록됨)"
} else {
    Write-Error "[sync-csproj] Assembly-CSharp.csproj 내에서 Compile ItemGroup 패턴을 찾지 못했습니다."
    exit 1
}
