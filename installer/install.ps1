param(
  [string]$Destination = (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'CodexProxyDesktop'),
  [string]$ShortcutPath = (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Codex 代理管理器.lnk')
)
$ErrorActionPreference = 'Stop'
try {
  $source = Join-Path $PSScriptRoot 'CodexProxyDesktop.exe'
  if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw '安装包缺少主程序。' }
  $target = Join-Path $Destination 'CodexProxyDesktop.exe'
  $running = Get-Process -Name 'CodexProxyDesktop' -ErrorAction SilentlyContinue | Where-Object { try { $_.Path -and [IO.Path]::GetFullPath($_.Path) -ieq [IO.Path]::GetFullPath($target) } catch { $false } }
  if ($running) { throw '请先从系统托盘退出 Codex 代理管理器，然后重新安装。' }
  New-Item -ItemType Directory -Path $Destination -Force | Out-Null
  Copy-Item -LiteralPath $source -Destination $target -Force
  if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) { throw '复制后校验失败。' }
  $shell = New-Object -ComObject WScript.Shell
  $shortcut = $shell.CreateShortcut($ShortcutPath)
  $shortcut.TargetPath = $target
  $shortcut.WorkingDirectory = $Destination
  $shortcut.Description = 'Codex 代理管理器'
  $shortcut.Save()
  Write-Host "安装完成：$target"
  Write-Host '已创建桌面快捷方式；原有设置和自启动状态保持不变。'
} catch {
  Write-Host ('安装失败：' + $_.Exception.Message) -ForegroundColor Red
  exit 1
}
