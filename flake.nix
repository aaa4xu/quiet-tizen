{
  description = "Quiet Tizen development shell";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      systems = [ "aarch64-darwin" "x86_64-darwin" "x86_64-linux" "aarch64-linux" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f (import nixpkgs {
        inherit system;
        # The launcher targets the TV's .NET 6 runtime; nixpkgs marks the end-of-life SDK insecure.
        config.permittedInsecurePackages = [ "dotnet-sdk-6.0.428" ];
      }));
    in {
      devShells = forAllSystems (pkgs: {
        default = pkgs.mkShell {
          packages = [
            # Host tools and tests; Pillow only for raster artwork conversion.
            (pkgs.python312.withPackages (ps: [ ps.pillow ]))
            # global.json pins the 6.0 feature band used for launcher and payload builds.
            pkgs.dotnet-sdk_6
            # Key generation for the temporary root session.
            pkgs.openssl
            # Inspecting registry snapshots pulled into .local/.
            pkgs.sqlite
          ];
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          # Samsung's SDB is not in nixpkgs; use the copy kept under .local/ when present.
          shellHook = ''
            if [ -z "''${SDB:-}" ] && [ -x .local/sdb/data/tools/sdb ]; then
              export SDB="$PWD/.local/sdb/data/tools/sdb"
            fi
          '';
        };
      });
    };
}
