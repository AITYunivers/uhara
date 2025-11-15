using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

public partial class Tools : MainShared
{
    public partial class ClickteamFusion
    {
        public partial class Instance
        {
            #region PUBLIC_API
            public void WatchGlobalVariable(string watcherName, int globalVarIndex)
            {
                try
                {
                    int[] ptr_cv = new int[] { 0x268, globalVarIndex * 4 };
                    uint cv = TMemory.ReadMemory<uint>(ProcessInstance, mVPointer, ptr_cv);

                    uint type = TMemory.ReadMemory<uint>(ProcessInstance, cv + 0x0);

                    PtrResolver ptrResolver = new PtrResolver();
                    if (type == 0) // Integer
                        ptrResolver.Watch<int>(watcherName, cv + 0x8);
                    else if (type == 1) // Double
                        ptrResolver.Watch<double>(watcherName, cv + 0x8);

                    // Store for type rechecking on update
                    cv_ptrResolvers.Add((watcherName, cv, type));
                }
                catch { }
            }

            public void WatchGlobalString(string watcherName, int globalStrIndex)
            {
                try
                {
                    new PtrResolver().WatchString(watcherName, mVPointer, 0x27C, globalStrIndex * 4, 0x8);
                }
                catch { }
            }

            public void WatchAlterableVariable(string watcherName, string objectName, int altVarIndex, int instanceIndex = 0)
            {
                try
                {
                    uint baseAddress = GetInstanceAddress(objectName, instanceIndex);
                    uint ptr_roObjType = 0x18;

                    short roObjType = TMemory.ReadMemory<short>(ProcessInstance, baseAddress + ptr_roObjType);
                    bool isSystem = roObjType != 2 && roObjType < 32;
                    if (isSystem && FusionBuild < 292)
                    {
                        TUtils.Print("System Objects cannot have Alterable Variables in " + FusionBuild);
                        return;
                    }

                    uint ptr_cv = isSystem ? 0x1F2u : 0x242u;
                    uint cv = TMemory.ReadMemory<uint>(ProcessInstance, baseAddress + ptr_cv) + (0x10 * (uint)altVarIndex);

                    uint type = TMemory.ReadMemory<uint>(ProcessInstance, cv + 0x0);

                    PtrResolver ptrResolver = new PtrResolver();
                    if (type == 0) // Integer
                        ptrResolver.Watch<int>(watcherName, cv + 0x8);
                    else if (type == 1) // Double
                        ptrResolver.Watch<double>(watcherName, cv + 0x8);

                    // Store for type rechecking on update
                    cv_ptrResolvers.Add((watcherName, cv, type));
                }
                catch { }
            }

            public void WatchAnimation(string watcherName, string objectName, int instanceIndex = 0)
            {
                try
                {
                    uint baseAddress = GetInstanceAddress(objectName, instanceIndex);
                    uint ptr_roObjType = 0x18;

                    short roObjType = TMemory.ReadMemory<short>(ProcessInstance, baseAddress + ptr_roObjType);
                    if (roObjType != 2)
                    {
                        TUtils.Print("Object must be an Active!");
                        return;
                    }

                    uint ptr_anim = 0x1DE;
                    new PtrResolver().Watch<int>(watcherName, baseAddress + ptr_anim);
                }
                catch { }
            }

            public void WatchCounter(string watcherName, string objectName, int instanceIndex = 0)
            {
                try
                {
                    uint baseAddress = GetInstanceAddress(objectName, instanceIndex);
                    uint ptr_roObjType = 0x18;

                    short roObjType = TMemory.ReadMemory<short>(ProcessInstance, baseAddress + ptr_roObjType);
                    if (roObjType < 5 || roObjType > 7)
                    {
                        TUtils.Print(objectName + " is not a counter!");
                        return;
                    }

                    uint ptr_cv = FusionBuild < 292 ? 0x202u : 0x2B0u;
                    uint cv = baseAddress + ptr_cv;
                    uint type = TMemory.ReadMemory<uint>(ProcessInstance, cv + 0x0);

                    PtrResolver ptrResolver = new PtrResolver();
                    if (type == 0) // Integer
                        ptrResolver.Watch<int>(watcherName, cv + 0x8);
                    else if (type == 1) // Double
                        ptrResolver.Watch<double>(watcherName, cv + 0x8);

                    // Store for type rechecking on update
                    cv_ptrResolvers.Add((watcherName, cv, type));
                    counterVariables.Add(watcherName);
                }
                catch { }
            }
            #endregion

            internal static uint mVPointer;
            internal int FusionBuild;
            internal Dictionary<string, List<short>> objectInfos = new Dictionary<string, List<short>>();
            internal List<(string, uint, uint)> cv_ptrResolvers = new List<(string, uint, uint)>();
            internal List<string> counterVariables = new List<string>();

            public Instance()
            {
                mVPointer = GetMvPointer();
                CacheObjectInfos();

                FusionBuild = TMemory.ReadMemory<int>(ProcessInstance, mVPointer, 0xC);
                new PtrResolver().Watch<int>("Frame", mVPointer, 0x1F0);
                new PtrResolver().Watch<int>("FrameCount", mVPointer, 0xC4);
                new PtrResolver().WatchString("FrameName", mVPointer + 4, 0x10, 0x0);

                Main.OnUpdate += OnUpdate;
            }

            private void OnUpdate()
            {
                for (int i = 0; i < cv_ptrResolvers.Count; i++)
                {
                    string watcherName = cv_ptrResolvers[i].Item1;
                    uint cvAddress = cv_ptrResolvers[i].Item2;
                    uint oldType = cv_ptrResolvers[i].Item3;

                    // Check if the type has changed
                    short currentType = TMemory.ReadMemory<short>(ProcessInstance, cvAddress + 0x0);
                    if (currentType == oldType)
                        continue;

                    // Remove old watcher
                    for (int ii = 0; ii < MemoryWatchers.Count; ii++)
                    {
                        if (MemoryWatchers[ii].Name != watcherName)
                            continue;
                        MemoryWatchers.RemoveAt(ii);
                        break;
                    }

                    // Add new watcher
                    PtrResolver ptrResolver = new PtrResolver();
                    if (currentType == 0) // Integer
                        ptrResolver.Watch<int>(watcherName, cvAddress + 0x8);
                    else if (currentType == 1) // Double
                        ptrResolver.Watch<double>(watcherName, cvAddress + 0x8);
                }

                foreach (string counterName in counterVariables)
                {
                    if (current.ContainsKey(counterName))
                    {
                        if (current[counterName] is int curInt)
                            current[counterName] = (curInt * -1) - 1;
                        else if (current[counterName] is double curDouble)
                            current[counterName] = (curDouble * -1) - 1;
                    }
                }
            }

            private uint GetMvPointer()
            {
                TUtils.Print("GetMvPointer Called");
                string headerBytes = "";
                foreach (byte b in Encoding.ASCII.GetBytes("PAMU"))
                    headerBytes += b.ToString("X2") + " ";

                uint header = 0;
                ulong[] results = TMemory.ScanPagesMultiple(ProcessInstance, headerBytes.Trim());
                TUtils.Print($"Found {results.Length} Headers with Sigscan {headerBytes.Trim()}");
                foreach (ulong item in results)
                {
                    //TUtils.Print($"Checking header: {item.ToString("X2")}");
                    int runtimeVersion = TMemory.ReadMemory<int>(ProcessInstance, item + 4);
                    if (runtimeVersion == 770)
                    {
                        TUtils.Print("Found Header at " + item.ToString("X2"));
                        header = (uint)item;
                        break;
                    }
                }

                TUtils.Print("Finished Header Scan");
                if (header == 0)
                    return 0;

                headerBytes = "";
                foreach (byte b in BitConverter.GetBytes((int)header))
                    headerBytes += b.ToString("X2") + " ";

                ulong[] headerResults = TMemory.ScanPagesMultiple(ProcessInstance, headerBytes.Trim());
                if (headerResults.Length == 0)
                {
                    TUtils.Print("Failed to find MV pointer");
                    return 0;
                }

                TUtils.Print($"Found {results.Length} possible MV pointers with Sigscan {headerBytes.Trim()}");

                uint low = (uint)ProcessInstance.MainModule.BaseAddress;
                uint high = low + (uint)ProcessInstance.MainModule.ModuleMemorySize;
                foreach (ulong item in headerResults)
                {
                    //TUtils.Print($"Checking possible MV pointer: {item.ToString("X2")}");
                    if (item < low || item > high)
                        continue;

                    TUtils.Print("Found MV pointer at " + item.ToString("X2"));
                    return (uint)item;
                }

                TUtils.Print("Failed to find MV pointer");
                return 0;
            }

            private void CacheObjectInfos()
            {
                int[] ptr_oiMaxIndex = new int[] { 0x190 };
                int[] ptr_ois = new int[] { 0x19C, 0x0 };
                uint ptr_oiHandle = 0x0;
                uint ptr_oiName = 0x10;

                int oiMaxIndex = TMemory.ReadMemory<int>(ProcessInstance, mVPointer, ptr_oiMaxIndex);
                TUtils.Print($"Found {oiMaxIndex} object infos");
                for (int i = 0; i < oiMaxIndex; i++)
                {
                    ptr_ois[1] = i * 4;
                    uint oiAddress = TMemory.ReadMemory<uint>(ProcessInstance, mVPointer, ptr_ois);
                    short oiHandle = TMemory.ReadMemory<short>(ProcessInstance, oiAddress + ptr_oiHandle);
                    string oiName = TMemory.ReadMemoryUnicodeString(ProcessInstance, TMemory.ReadMemory<uint>(ProcessInstance, oiAddress + ptr_oiName));

                    //TUtils.Print($"Found \"{oiName}\" with handle {oiHandle} at index {i}");
                    if (objectInfos.ContainsKey(oiName))
                        objectInfos[oiName].Add(oiHandle);
                    else
                        objectInfos.Add(oiName, new List<short>() { oiHandle });
                }
            }

            private uint GetInstanceAddress(string objectName, int instanceCount = 0)
            {
                int[] ptr_roMaxIndex = new int[] { 0x8F2 };
                int[] ptr_ros = new int[] { 0x8D0, 0x0 };
                uint ptr_roObjInfo = 0x12;
                List<short> objInfos = objectInfos[objectName];

                int roMaxIndex = TMemory.ReadMemory<int>(ProcessInstance, mVPointer + 4, ptr_roMaxIndex);
                for (int i = 0; i < roMaxIndex; i++)
                {
                    ptr_ros[1] = i * 8;
                    uint roAddress = TMemory.ReadMemory<uint>(ProcessInstance, mVPointer + 4, ptr_ros);
                    short roObjInfo = TMemory.ReadMemory<short>(ProcessInstance, roAddress + ptr_roObjInfo);

                    if (objInfos.Contains(roObjInfo))
                    {
                        if (instanceCount == 0)
                            return roAddress;
                        else instanceCount--;
                    }
                }

                return 0;
            }
        }
    }
}
