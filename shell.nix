{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  buildInputs = [
    pkgs.dotnet-sdk_8
    pkgs.fontconfig
    pkgs.freetype
    pkgs.libX11
    pkgs.libXcursor
    pkgs.libXrandr
    pkgs.libXi
    pkgs.libICE
    pkgs.libSM
    pkgs.libXext
    pkgs.libXfixes
    pkgs.libXrender
    pkgs.libGL
  ];

  shellHook = ''
    export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath [
      pkgs.fontconfig
      pkgs.freetype
      pkgs.libX11
      pkgs.libXcursor
      pkgs.libXrandr
      pkgs.libXi
      pkgs.libICE
      pkgs.libSM
      pkgs.libXext
      pkgs.libXfixes
      pkgs.libXrender
      pkgs.libGL
    ]}:$LD_LIBRARY_PATH"
  '';
}
