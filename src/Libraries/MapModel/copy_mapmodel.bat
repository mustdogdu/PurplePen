rem @echo off
if "%1"=="" goto MissingArg

echo "Copying..."
copy "..\..\..\..\..\MapModel\%1\src\MapModel\bin\Debug\MapModel.dll" Debug
copy "..\..\..\..\..\MapModel\%1\src\MapModel\bin\Debug\MapModel.pdb" Debug
copy "..\..\..\..\..\MapModel\%1\src\MapModel\bin\Release\MapModel.dll" Release
copy "..\..\..\..\..\MapModel\%1\src\MapModel\bin\Release\MapModel.pdb" Release
copy "..\..\..\..\..\MapModel\%1\src\Map_GDIPlus\bin\Debug\Map_GDIPlus.dll" Debug
copy "..\..\..\..\..\MapModel\%1\src\Map_GDIPlus\bin\Debug\Map_GDIPlus.pdb" Debug
copy "..\..\..\..\..\MapModel\%1\src\Map_GDIPlus\bin\Release\Map_GDIPlus.dll" Release
copy "..\..\..\..\..\MapModel\%1\src\Map_GDIPlus\bin\Release\Map_GDIPlus.pdb" Release
copy "..\..\..\..\..\MapModel\%1\src\PdfSharp\bin\Debug\PdfSharp.dll" Debug
copy "..\..\..\..\..\MapModel\%1\src\PdfSharp\bin\Debug\PdfSharp.pdb" Debug
copy "..\..\..\..\..\MapModel\%1\src\PdfSharp\bin\Release\PdfSharp.dll" Release
copy "..\..\..\..\..\MapModel\%1\src\Graphics2D\bin\Debug\Graphics2D.dll" Debug
copy "..\..\..\..\..\MapModel\%1\src\Graphics2D\bin\Debug\Graphics2D.pdb" Debug
copy "..\..\..\..\..\MapModel\%1\src\Graphics2D\bin\Release\Graphics2D.dll" Release
copy "..\..\..\..\..\MapModel\%1\src\Graphics2D\bin\Release\Graphics2D.pdb" Release

goto End

:MissingArg
Echo "Pass name of branch to copy from (e.g., "trunk")
goto End

:End