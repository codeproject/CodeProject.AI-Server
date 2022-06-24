REM Assuming we're in the /docs/mkdocs directory

pushd CodeProject.AI\docs

REM Recursively the files from .html to .aspx
 cd site
 FOR /R %x IN (*.html) DO ren "%x" *.aspx

REM Replace all ".html" with ".aspx" in the .aspx files
 PowerShell ^
     $s = ".html"; ^
     $r = ".aspx"; ^
     Get-ChildItem "." -Recurse -Filter *.aspx | ForEach-Object {
         (Get-Content $_.FullName) | ^
             ForEach-Object { $_ -replace [regex]::Escape($s), $r } | Set-Content $_.FullName ^
     }

popd
