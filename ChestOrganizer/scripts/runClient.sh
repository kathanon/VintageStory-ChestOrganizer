#!/usr/bin/env bash

VINTAGE_STORY=$1
PROJECT_DIR=$2
CONFIGURATION=$3

cd ${VINTAGE_STORY}
./Vintagestory \
   --playStyle "creativebuilding" \
   --openWorld "chestorgcreative" \
   --addModPath ${PROJECT_DIR}/bin/${CONFIGURATION}/Mods \
   --dataPath $VS_DEV_DATA \
   --addOrigin ${PROJECT_DIR}/assets \
| grep -v  'after final compo   - OpenGL threw an error'
