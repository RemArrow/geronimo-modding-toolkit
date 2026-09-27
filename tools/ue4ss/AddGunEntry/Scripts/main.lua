--[[
  Add a NEW gun entry to Geronimo's spawner UI, and probe whether custom packages load.

  Why it works this way: Geronimo builds its gun list at runtime from the AssetRegistry,
  which is frozen inside the shipped pak, and BP_GameInstance::LoadMods() is an empty stub.
  New package IDs also never register (see docs/findings.md). So instead of shipping a
  cooked DataAsset, this constructs a GunAsset at RUNTIME with StaticConstructObject --
  a runtime UObject needs no package, which sidesteps the blocker entirely.

  The entry is genuinely additive: nothing is removed or replaced, the list grows by one.
  Its GEOMETRY, however, comes entirely from DONOR's ReceiverClass blueprint, because
  GunAsset exposes no mesh field. To make it look like something else you must override
  the donor's mesh package -- which changes the donor too. That is a hard limit of this
  build, not an oversight.

  Output goes to ue4ss\UE4SS.log, readable from outside the game. In a shipping build
  that is the only diagnostic channel that works.
--]]

-------------------------------------------------------------------------------- config
local CATEGORY = 0            -- 0 = Assault Rifle
local NEW_NAME = "My Custom Gun"
local OBJ_NAME = "DA_MyCustomGun"

-- Any existing GunAsset to copy ReceiverClass / Icon / bEnabled from.
local DONOR = "/Game/Weapons/Data/DA_HK416a5_279.DA_HK416a5_279"

-- GUID-suffixed field inside FGunAssetList. Re-check with a property dump if the game updates.
local FIELD = "GunAssets_9_E68A935B442B9DB0A278E7B5893C9566"

-- Packages to probe. CONTROL entries MUST be real game packages: without them a failure
-- is ambiguous between "the package didn't load" and "the probe itself doesn't work".
local CONTROL = {
  {"GAME mesh",  "/Game/Weapons/HK416a5/Mesh/HK_416_279_v2.HK_416_279_v2"},
  {"GAME data",  DONOR},
}
local MINE = {
  -- {"MINE mesh", "/Game/Weapons/YourMod/Mesh/YourMesh.YourMesh"},
}
--------------------------------------------------------------------------------

local function log(m) print("[AddGunEntry] " .. tostring(m) .. "\n") end
local function valid(o)
  return o ~= nil and type(o) ~= "boolean" and o.IsValid ~= nil and o:IsValid()
end

local function tryLoad(label, path)
  local find, load = nil, nil
  pcall(function() find = StaticFindObject(path) end)
  pcall(function() load = LoadAsset(path) end)          -- must be on the game thread
  local after = nil
  pcall(function() after = StaticFindObject(path) end)
  log(string.format("  %-12s find=%-4s load=%-4s after=%-4s  %s",
      label, valid(find) and "yes" or "no", valid(load) and "yes" or "no",
      valid(after) and "YES" or "no", path))
  return valid(after)
end

local function probe()
  if #CONTROL == 0 and #MINE == 0 then return end
  log("==== CONTROLS (game packages) ====")
  local okc = 0
  for _, e in ipairs(CONTROL) do if tryLoad(e[1], e[2]) then okc = okc + 1 end end
  local oko = 0
  if #MINE > 0 then
    log("==== MINE (new packages) ====")
    for _, e in ipairs(MINE) do if tryLoad(e[1], e[2]) then oko = oko + 1 end end
  end
  log(string.format("==== VERDICT: controls %d/%d, mine %d/%d ====", okc, #CONTROL, oko, #MINE))
  if okc == 0 then
    log("     LoadAsset fails even for GAME packages - the probe is at fault, not your mod")
  elseif #MINE > 0 and oko == 0 then
    log("     Game packages load, yours do not - new packages do not register (expected)")
  end
end

local function dumpProperties(obj)
  log("==== GunAsset properties ====")
  local ok = pcall(function()
    obj:GetClass():ForEachProperty(function(p)
      log(string.format("  %-42s : %s",
          p:GetFName():ToString(), p:GetClass():GetFName():ToString()))
    end)
  end)
  if not ok then log("  ForEachProperty unavailable in this UE4SS build") end
end

-- AvailableGuns is a TMap<EGunType, FGunAssetList>. map:Find() returns an unusable
-- value here; ForEach + :get() is the only reliable way to reach the live struct.
local function categoryArray(map)
  local found = nil
  map:ForEach(function(k, v)
    local kv = k; pcall(function() kv = k:get() end)
    if tonumber(tostring(kv)) == CATEGORY and found == nil then
      local vv = v; pcall(function() vv = v:get() end)
      pcall(function() found = vv[FIELD] end)
    end
  end)
  return found
end

local added = false
local function Run()
  ExecuteInGameThread(function()                        -- LoadAsset requires the game thread
    probe()
    if added then log("entry already added") return end

    local gi = FindFirstOf("BP_GameInstance_C")
    if not valid(gi) then log("no GameInstance yet - retry with F8") return end
    local donor = StaticFindObject(DONOR)
    if not valid(donor) then log("donor not found: " .. DONOR) return end
    dumpProperties(donor)

    local arr = categoryArray(gi.AvailableGuns)
    if arr == nil then log("category " .. CATEGORY .. " not found in AvailableGuns") return end

    local cls = StaticFindObject("/Game/Gameplay/Customization/GunAsset.GunAsset_C")
    if not valid(cls) then log("GunAsset_C class not found") return end

    local newGun = nil
    pcall(function() newGun = StaticConstructObject(cls, gi, FName(OBJ_NAME)) end)
    if not valid(newGun) then log("StaticConstructObject failed") return end

    for _, p in ipairs({"ReceiverClass", "Icon", "bEnabled"}) do
      pcall(function() newGun[p] = donor[p] end)
    end
    pcall(function() newGun.Category    = CATEGORY end)
    pcall(function() newGun.DisplayName = FText(NEW_NAME) end)

    local n = arr:GetArrayNum()
    arr[n + 1] = newGun                                  -- append via index assignment
    added = arr:GetArrayNum() > n
    log(added and string.format("*** ADDED '%s' (%d -> %d) ***", NEW_NAME, n, arr:GetArrayNum())
              or  "append failed")
  end)
end

RegisterKeyBind(Key.F8, Run)
log("loaded - adds '" .. NEW_NAME .. "' (F8 to re-run)")
ExecuteWithDelay(20000, function() pcall(Run) end)       -- let the GameInstance come up first
