using System;

namespace RayFire
{
	[Serializable]
	public class RFFragmentProperties
	{
		public bool           rem; 
		public bool           dec; // Decompose
		public SliceType      slc;
		public bool           cmb; // Combine
		public bool           cap; // Input Cap
		public bool           ptr; // Petrify
		public RFColliderType col;
		public float          szF;
		public bool           l; // Inherit layer
		public int            lay;
		public bool           t; // Inherit tag
		public string         tag;

		/// /////////////////////////////////////////////////////////
        /// Constructor
        /// /////////////////////////////////////////////////////////
		
		// Constructor
		public RFFragmentProperties()
		{
			InitValues();
		}
		
		// Starting values
		public void InitValues()
		{
			rem = false;
			slc = SliceType.Hybrid;
			cmb = true;
			dec = false;
			cap = true;
			ptr = true;
			col = RFColliderType.Mesh;
			szF = 0;
			l   = true;
			lay = 0;
			t   = true;
			tag = string.Empty;
		}

		// Copy from
		public void CopyFrom (RFFragmentProperties props)
		{
			rem = props.rem;
			slc = props.slc;
			cmb = props.cmb;
			dec = props.dec;
			cap = props.cap;
			ptr = props.ptr;
			col = props.col;
			szF = props.szF;
			l   = props.l;
			lay = props.lay;
			t   = props.t;
			tag = props.tag;
		}
		
		/// /////////////////////////////////////////////////////////
		/// Layer & Tag
		/// /////////////////////////////////////////////////////////
        
		// Get layer for fragments
		public static int GetLayer (RayfireRigid scr)
		{
			// Inherit layer
			if (scr.mshDemol.prp.l == true)
				return scr.gameObject.layer;

			// Get custom layer
			return scr.mshDemol.prp.lay;
		}
        
		// Set layer for fragments
		public static void SetLayer (RayfireRigid scr)
		{
			if (scr.mshDemol.prp.l == false)
			{
				int baseLayer = GetLayer(scr);
				for (int i = 0; i < scr.fragments.Count; i++)
					scr.fragments[i].gameObject.layer = baseLayer;
				
				if (scr.objTp == ObjectType.ConnectedCluster)
				{
					for (int i = 0; i < scr.clsDemol.cluster.shards.Count; i++)
						scr.clsDemol.cluster.shards[i].tm.gameObject.layer = baseLayer;
				}
			}
		}
		
		// Get tag for fragments
		public static string GetTag (RayfireRigid scr)
		{
			// Inherit tag
			if (scr.mshDemol.prp.t == true)
				return scr.gameObject.tag;
            
			// Set tag. Not defined -> Untagged
			if (scr.mshDemol.prp.tag.Length == 0)
				return "Untagged";
			
			// Set tag.
			return scr.mshDemol.prp.tag;
		}
		
		// Set tag for fragments
		public static void SetTag (RayfireRigid scr)
		{
			if (scr.mshDemol.prp.t == false)
			{
				string baseTag = GetTag(scr);
				for (int i = 0; i < scr.fragments.Count; i++)
					scr.fragments[i].gameObject.tag = baseTag;
				
				if (scr.objTp == ObjectType.ConnectedCluster)
				{
					for (int i = 0; i < scr.clsDemol.cluster.shards.Count; i++)
						scr.clsDemol.cluster.shards[i].tm.gameObject.tag = baseTag;
				}
			}
		}
		
		// Get Combine state. Cant be true if decompose enabled
		public bool Combine { get
		{
			return cmb == true && dec == false;
		}}
	}
}