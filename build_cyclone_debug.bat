pushd %~dp0cyclonedds
::if exist build\CMakeCache.txt (
::    echo Cleaning CMake cache...
::    del /F /Q build\CMakeCache.txt 2>nul
::    rmdir /S /Q build\CMakeFiles 2>nul
::)
mkdir build
cd build
cmake -A x64 -DCMAKE_INSTALL_PREFIX=%~dp0cyclone-compiled-debug ..
cmake --build . --target install --config Debug
popd
