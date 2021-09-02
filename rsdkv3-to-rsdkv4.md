# RSDKv3 to RSDKv4 Script conversion

Notes on how to convert RSDKv3 scripts to RSDKv4.

## CopyPalette

|||
|--|--
RSDKv3|`CopyPalette(srcPaletteId, dstPaletteId)`
RSDKv4|`CopyPalette(srcPaletteId, srcStart, dstPaletteId, dstStart, colorCount)`

To have a matching behaviour, replace `CopyPalette(XX,YY)` with `CopyPalette(XX,0,YY,0,256)`

## RotatePalette

|||
|--|--
RSDKv3| `RotatePalette(start, end, isRotatingRight)`

RSDKv4| `RotatePalette(paletteId, start, end, isRotatingRight)`

To have a matching behaviour, replace `RotatePalette(XX,YY,ZZ)` with `RotatePalette(0,XX,YY,ZZ)`

## PlayerObjectCollision

|||
|--|--
RSDKv3| `PlayerObjectCollision(type, left, top, right, bottom)`
RSDKv4| `BoxCollisionTest(type, entity1, left1, top1, right1, bottom1, entity2, left2, top2, right2, bottom2)`

In RSDKv3 it is hardcoded that the collision check is performed between the object that is currently invoking the function (eg. `entity1`) with the player (eg. `entity2`). While RSDKv4 adds more flexibility by allowing any script to check the collision between any entity to any entity. RSDKv4 also takes in consideration the fact that a collision test of an entity can be performed for multiple entites, including multiple players.

RSDKv3 version:

```rsdk
PlayerObjectCollision(TYPE, LEFT, TOP, RIGHT, BOTTOM)
```

RSDKv4 version:

```rsdk
foreach (GROUP_PLAYERS, currentPlayer, ACTIVE_ENTITIES)
    BoxCollisionTest(TYPE, object.entityPos, LEFT, TOP, RIGHT, BOTTOM, currentPlayer, object[currentPlayer].value40, object[currentPlayer].value38, object[currentPlayer].value41, object[currentPlayer].value39)
    // The variable CheckResult is now populated for the current entity
next
```

## Stage properties

|RSDKv3|RSDKv4|
|--|--
`Stage.XBoundary1`|`stage.curXBoundary1`
`Stage.YBoundary1`|`stage.curYBoundary1`
`Stage.XBoundary2`|`stage.curXBoundary2`
`Stage.YBoundary2`|`stage.curYBoundary2`

## Object properties

|RSDKv3|RSDKv4|
|--|--
`Object.XVelocity`|`Object.xvel`
`Object.YVelocity`|`Object.yvel`
`Object.EntityNo`|`Object.EntityPos`
`Object.TrackScroll`|`Object.scrollTracking`

## Player object and properties

|RSDKv3|RSDKv4|
|--|--
`Player.Timer`|`object.value1`
`Player.ObjectInteraction`|`object.interaction`

I have no clue why `Timer` became `value1`...

Also remember that using `object.` instead of `Player.` works only if you are in the `PlayerObject.txt` script. Otherwise you need to cycle between all the players with `foreach (GROUP_PLAYERS, currentPlayer, ACTIVE_ENTITIES)`, as RSDKv4 supports multiple players.

## Object[24]

`24` is the HUD object. When you want to retrieve the HUD object then it's recommended to do the following:

```rsdk
foreach (TypeName[HUD], arrayPos0, ALL_ENTITIES)
    object[arrayPos0] // <-- your logic here
next
```

## TempValue*

|RSDKv3|RSDKv4|
|--|--
`TempValue0`| `temp0`
`TempValue1`| `temp1`
`TempValue2`| `temp2`
`TempValue3`| `temp3`
`TempValue4`| `temp4`
`TempValue5`| `temp5`
`TempValue6`| `temp6`
`TempValue7`| `temp7`
