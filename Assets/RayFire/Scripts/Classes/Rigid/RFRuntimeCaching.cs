using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace RayFire
{
    [Serializable]
    public class RFRuntimeCaching
    {
        [FormerlySerializedAs ("type")]                public CachingType tp;
        [FormerlySerializedAs ("frames")]              public int         frm;
        [FormerlySerializedAs ("fragments")]           public int         frg;
        [FormerlySerializedAs ("skipFirstDemolition")] public bool        skp;
        public bool obj = false;

        [NonSerialized] public bool inProgress;
        [NonSerialized] public bool wasUsed;
        [NonSerialized] public bool stop;
           
        /// /////////////////////////////////////////////////////////
        /// Constructor
        /// /////////////////////////////////////////////////////////
        
        // Constructor
        public RFRuntimeCaching()
        {
            InitValues();
        }
        
        // Starting values
        public void InitValues()
        {
            tp  = CachingType.Disabled;
            frm = 3;
            frg = 4;
            skp = false;
        }
        
        // Copy from
        public void CopyFrom (RFRuntimeCaching rc)
        {
            tp  = rc.tp;
            frm = rc.frm;
            frg = rc.frg;
            skp = rc.skp;
        }
        
        /// /////////////////////////////////////////////////////////
        /// Static
        /// /////////////////////////////////////////////////////////
        
        // Get batch list
        public static List<int> GetBatchAmount(RayfireRigid rg, int num)
        {
            return rg.mshDemol.ch.tp == CachingType.ByFrames ? GetBatchByFrames (rg.mshDemol.ch.frm,  num) : GetBatchByFragments (rg.mshDemol.ch.frg, num);
        }
        
        // Get batches amount for continuous fragmentation
        static List<int> GetBatchByFrames (int frames, int amount)
        {
            // Get basic list
            int       div         = amount / frames;
            List<int> batchAmount = new List<int>(frames + 1);
            for (int i = 0; i < frames; i++)
                batchAmount.Add (div);

            // Consider difference
            int dif = amount % frames;
            if (dif > 0)
                for (int i = 0; i < dif; i++)
                    batchAmount[i] += 1;

            // Remove 0
            if (frames > amount)
                for (int i = batchAmount.Count - 1; i >= 0; i--)
                    if (batchAmount[i] == 0)
                        batchAmount.RemoveAt (i);
            
            return batchAmount;
        }
        
        // Get batches amount for continuous fragmentation
        static List<int> GetBatchByFragments (int fragments, int amount)
        {
            // Get basic list
            int       steps         = amount / fragments;
            List<int> batchAmount = new List<int>(steps + 1);
            if (steps > 0)
                for (int i = 0; i < steps; i++)
                    batchAmount.Add (fragments);

            // Consider difference
            int dif = amount % fragments;
            if (dif > 0)
                batchAmount.Add (dif);
            
            return batchAmount;
        }

        // Get list of marked elements index
        public static List<int> GetMarkedElements (int batchInd, List<int> batchAmount)
        {
            // Get offset
            int offset = 0;
            if (batchInd > 0)
                for (int i = 0; i < batchInd; i++)
                    offset += batchAmount[i];
            
            // Collect marked elements ids
            List<int> markedElements = new List<int>(batchAmount[batchInd]);
            for (int i = 0; i < batchAmount[batchInd]; i++)
                markedElements.Add (i + offset);

            return markedElements;
        }

        // Create tm reference
        public static GameObject CreateTmRef(RayfireRigid rfScr)
        {
            GameObject go = new GameObject("RFTempGo");
            go.SetActive (false);
            go.transform.position = rfScr.tsf.position;
            go.transform.rotation = rfScr.tsf.rotation;
            go.transform.localScale = rfScr.tsf.localScale;
            go.transform.parent = RayfireMan.inst.transform;
            return go;
        }

        // Prepare Rigid for caching
        public static bool PreOps(RayfireRigid scr)
        {
            // Caching in progress
            scr.mshDemol.ch.inProgress = true;
            
            // Object should be demolished when cached all meshes but not during caching
            bool demolitionShouldLocal = scr.lim.demolitionShould == true;
            scr.lim.demolitionShould = false;
            
            return demolitionShouldLocal;
        }
        
        // Post caching ops
        public static void PostOps(RayfireRigid scr, bool demolitionShouldLocal)
        {
            // Set demolition ready state and Add to demolition cor
            if (scr.mshDemol.ch.skp == false && demolitionShouldLocal == true)
            {
                scr.lim.demolitionShould = true;
                RayfireMan.inst.AddToDemolitionCor(scr);
            }
            
            // Reset damage
            if (scr.mshDemol.ch.skp == true && demolitionShouldLocal == true)
                scr.damage.LocalReset();
            
            // Caching finished
            scr.mshDemol.ch.inProgress = false;
            scr.mshDemol.ch.wasUsed    = true;
        }
        
        /// /////////////////////////////////////////////////////////
        /// Methods
        /// /////////////////////////////////////////////////////////
        
        // Stop runtime caching and reset it
        public void StopRuntimeCaching()
        {
            if (inProgress == true)
                stop = true;
        }
         
        public void Stop()
        {
            stop       = false;
            inProgress = false;
        }
         
        public bool MeshCacheState { get { return tp != CachingType.Disabled; } }
        public bool FragCacheState { get { return tp != CachingType.Disabled && obj == true; } }
        
    }
}