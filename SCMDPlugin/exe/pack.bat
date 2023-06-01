set me=%~dp0
set in=%~1
set out="%me%..\bin.h"

echo Putting "%in%GUILib.dll" to bin.h

echo #pragma warning(disable:4838) > %out%
echo #pragma warning(disable:4309) >> %out%

type "%in%GUILib.dll" | "%me%\bin2h.exe" -c guilib >> %out%
