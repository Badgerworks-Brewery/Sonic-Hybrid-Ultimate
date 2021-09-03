# RSDKv3 to RSDKv4 Script conversion

Notes on how to convert RSDKv3 scripts to RSDKv4.

## Looping through the `Object` array

In RSDKv3 there is a very dirty way to loop through objects. For example look at this code snippet:

```rsdk
while ArrayPos0<1056
    if Object[ArrayPos0].Type==TypeName[AmyRose]
        [...]
    end if
    ArrayPos0++
loop
```

The script hardcodes that it has to scroll through `1056` entities, check if any one of them is `TypeName[AmyRose]` and if that is the case it would perform a certain logic. If `TypeName[AmyRose]` is at `ArrayPos0 = 3`, the `while` loop will still continue to scroll until the 1056th element. This piece of logic is extremely slow via script.

In RSDKv4 there is a new loop expression called `foreach`. Take this code snippet as an example:

```rsdk
foreach (TypeName[AmyRose], ArrayPos0, ALL_ENTITIES)
    [...]
next
```

In this case the engine will perform a fast filtering across all the entities by `TypeName[AmyRose]`. Therefore the script will just scroll through the found entities.

## Multi-player support

RSDKv3 was not built with multi-player support in mind. You will essentially find two key components: the object `Player` and the function `PlayerObjectCollision`. Those are hardcoded to perform actions to the sole single player in the stage. For example if in RSDKv3 we perform `Player.Animation = ANI_HURT` how can you tell to set it to Tails rather than Sonic? Let's use this code as an example:

```rsdk
PlayerObjectCollision(C_TOUCH, -16, -16, 16, 16)
if CheckResult == 1
    Player.Animation = ANI_HURT
end if
```

RSDKv4 enables multi-player support and it is not limited to just Sonic & Tails. It is technically possible to make it work with N players. To achieve that you need to scroll each player entity and apply logic to them. In the example below, between the N amount of players currently on stage, only the one that actually collided will change animation:

```rsdk
foreach (GROUP_PLAYERS, currentPlayer, ACTIVE_ENTITIES)
    BoxCollisionTest(C_TOUCH, object.entityPos, -16, -16, 16, 16, currentPlayer, HITBOX_AUTO, HITBOX_AUTO, HITBOX_AUTO, HITBOX_AUTO)
    if CheckResult == 1
        object[currentPlayer].Animation = ANI_HURT
    end if
next
```

## Properties

|RSDKv3|RSDKv4|
|--|--
`Screen.CenterX`|`screen.xcenter`
`Screen.CenterY`|`screen.ycenter`
`Stage.XBoundary1`|`stage.curXBoundary1`
`Stage.YBoundary1`|`stage.curYBoundary1`
`Stage.XBoundary2`|`stage.curXBoundary2`
`Stage.YBoundary2`|`stage.curYBoundary2`
`Object.XVelocity`|`Object.xvel`
`Object.YVelocity`|`Object.yvel`
`Object.EntityNo`|`Object.EntityPos`
`Object.TrackScroll`|`Object.scrollTracking`
`Player.Timer`|`object.value1`
`Player.ObjectInteraction`|`object.interaction`
`TempValue0`| `temp0`
`TempValue1`| `temp1`
`TempValue2`| `temp2`
`TempValue3`| `temp3`
`TempValue4`| `temp4`
`TempValue5`| `temp5`
`TempValue6`| `temp6`
`TempValue7`| `temp7`

Also remember that using `object.` instead of `Player.` works only if you are in the `PlayerObject.txt` script. Otherwise you need to cycle between all the players with `foreach (GROUP_PLAYERS, currentPlayer, ACTIVE_ENTITIES)`, as RSDKv4 supports multiple players.

## Function calls

|RSDKv3|RSDKv4|
|--|--
`PlaySfx(22,0)`|`PlaySfx(SfxName[Boss Hit], 0)`

## CopyPalette

|Engine|Function|
|--|--
RSDKv3|`CopyPalette(srcPaletteId, dstPaletteId)`
RSDKv4|`CopyPalette(srcPaletteId, srcStart, dstPaletteId, dstStart, colorCount)`

To have a matching behaviour, replace `CopyPalette(XX,YY)` with `CopyPalette(XX,0,YY,0,256)`

## RotatePalette

|Engine|Function|
|--|--
RSDKv3| `RotatePalette(start, end, isRotatingRight)`

RSDKv4| `RotatePalette(paletteId, start, end, isRotatingRight)`

To have a matching behaviour, replace `RotatePalette(XX,YY,ZZ)` with `RotatePalette(0,XX,YY,ZZ)`

## PlayerObjectCollision

|Engine|Function|
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

## Object[24]

`24` is the HUD object. When you want to retrieve the HUD object then it's recommended to do the following:

```rsdk
foreach (TypeName[HUD], arrayPos0, ALL_ENTITIES)
    object[arrayPos0] // <-- your logic here
next
```
