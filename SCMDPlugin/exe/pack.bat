set me=%~dp0
set in=%~1
set root=%~2
set out="%me%..\bin.h"

echo Running ILMerge

@rem "%root%packages\ILMerge.3.0.41\tools\net452\ILMerge.exe" "%in%olds/EntityFramework.dll" /out:"%in%Map Repository/StarcraftMapRepository.dll" "%in%olds/EntityFramework.SqlServer.dll" "%in%olds/Newtonsoft.Json.dll" "%in%olds/System.Data.SQLite.dll" "%in%olds/System.Data.SQLite.EF6.dll" "%in%olds/System.Data.SQLite.Linq.dll"

"%root%packages\ILMerge.3.0.41\tools\net452\ILMerge.exe" "%in%Community.CsharpSqlite.dll" /out:"%in%Map Repository/StarcraftMapRepository.dll" "%in%Community.CsharpSqlite.SQLiteClient.dll"

echo Putting "%in%GUILib.dll" to bin.h

echo #pragma warning(disable:4838) > %out%
echo #pragma warning(disable:4309) >> %out%

type "%in%GUILib.dll" | "%me%\bin2h.exe" -c guilib >> %out%
type "%in%Map Repository\StarcraftMapRepository.dll" | "%me%\bin2h.exe"  -c libs >> %out%