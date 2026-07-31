#!/bin/bash
# Generates golden OID JSON files from net-snmp's snmptranslate.
# Run from WSL: bash /mnt/c/Projects/snmpsharp/tests/generate-golden-oids.sh

set -e

MIB_DIR="/mnt/c/Projects/snmpsharp/tests/mibs"
OUT_DIR="/mnt/c/Projects/snmpsharp/tests/golden-oids"
mkdir -p "$OUT_DIR"

# List of MIBs to generate golden files for (filename without .mib extension)
MIBS=(
    "IANAifType-MIB"
    "IF-MIB"
    "WESTERMO-OID-MIB"
    "WESTERMO-INTERFACE-MIB"
    "SNMP-FRAMEWORK-MIB"
    "IANA-ADDRESS-FAMILY-NUMBERS-MIB"
    "LLDP-MIB"
    "BRIDGE-MIB"
    "ENTITY-MIB"
    "MAU-MIB"
)

# Load all MIBs at once so cross-references resolve
ALL_MIBS="+ALL"

for MIB in "${MIBS[@]}"; do
    echo "Generating $MIB..."
    OUT_FILE="$OUT_DIR/$MIB.json"

    # Get all symbols defined in this module
    SYMBOLS=$(snmptranslate -m "$ALL_MIBS" -M "+$MIB_DIR" -TB '.*' 2>/dev/null | grep "^${MIB}::" | sed "s/^${MIB}:://")

    echo "{" > "$OUT_FILE"
    FIRST=true
    while IFS= read -r sym; do
        [ -z "$sym" ] && continue
        OID=$(snmptranslate -m "$ALL_MIBS" -M "+$MIB_DIR" -On "${MIB}::${sym}" 2>/dev/null)
        if [ -n "$OID" ]; then
            # Strip leading dot
            OID="${OID#.}"
            if [ "$FIRST" = true ]; then
                FIRST=false
            else
                echo "," >> "$OUT_FILE"
            fi
            printf '  "%s": "%s"' "$sym" "$OID" >> "$OUT_FILE"
        fi
    done <<< "$SYMBOLS"
    echo "" >> "$OUT_FILE"
    echo "}" >> "$OUT_FILE"
    echo "  -> $(grep -c '"' "$OUT_FILE" | head -1) entries"
done

echo "Done! Golden files in $OUT_DIR"
