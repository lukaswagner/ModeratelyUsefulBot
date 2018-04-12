#!/bin/bash
./ModeratelyUsefulBot
while [ $? -eq 1 ]; do
   ./ModeratelyUsefulBot
done
