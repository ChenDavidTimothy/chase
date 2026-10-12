using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Release gate for the 31KB, zero-simulation menu choreography.
/// Re-record intentionally from real physics via ChaseMenuReplayBaker.
/// </summary>
public static class ChaseMenuReplayContract
{
    const string Path="Assets/Resources/MenuChaseReplay.bytes";
    public static void Validate()
    {
        var still=AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Resources/MenuArenaStill.jpg");
        if(still==null || still.width!=1280 || still.height!=720)
            throw new InvalidDataException(
                "Missing validated, lightweight Arena 01 still backdrop");
        var file=AssetDatabase.LoadAssetAtPath<TextAsset>(Path);
        if(file==null)
            throw new InvalidOperationException("Missing real-physics menu showcase asset");
        int[] required = new int[12];
        using(var stream=new MemoryStream(file.bytes,false))
        using(var reader=new BinaryReader(stream))
        {
            int magic=reader.ReadInt32();
            int hertz=reader.ReadInt32();
            int shots=reader.ReadInt32();
            if(magic!=0x43485231||hertz!=50||shots!=3)
                throw new InvalidDataException("Invalid menu recording envelope");
            for(int shot=0;shot<shots;shot++)
            {
                int frames=reader.ReadInt32();
                if(frames<150||frames>300)
                    throw new InvalidDataException("Invalid showcase duration");
                Vector3[] previous=new Vector3[2];
                for(int frame=0;frame<frames;frame++)
                    for(int actor=0;actor<2;actor++)
                    {
                        var pos=new Vector3(reader.ReadSingle(),
                            reader.ReadSingle(),reader.ReadSingle());
                        float yaw=reader.ReadSingle();
                        byte action=reader.ReadByte();
                        float phase=reader.ReadSingle();
                        if(!float.IsFinite(pos.x)||!float.IsFinite(pos.y)||
                            !float.IsFinite(pos.z)||!float.IsFinite(yaw)||
                            !float.IsFinite(phase)||action>=required.Length)
                            throw new InvalidDataException("NaN/unknown baked Chibi pose");
                        if(Mathf.Abs(pos.x)>6.25f || Mathf.Abs(pos.z)>6.25f ||
                            pos.y<.35f||pos.y>3.5f)
                            throw new InvalidDataException(
                                "Recorded character leaves playable arena");
                        if(frame>0&&Vector3.Distance(previous[actor],pos)>.52f)
                            throw new InvalidDataException(
                                "Unphysical intra-shot teleport detected");
                        previous[actor]=pos;
                        if(actor==0)required[action]++;
                        else
                        {
                            if(Vector3.Distance(previous[0],pos)<.76f)
                                throw new InvalidDataException(
                                    "Showcase contact is not a valid evade at shot="+
                                    shot+" frame="+frame);
                            if(action==1)required[1]++;
                        }
                    }
            }
            if(stream.Position!=stream.Length)
                throw new InvalidDataException("Unexpected replay trailing bytes");
        }
        if(required[3]<20||required[4]<12||
            required[5]<5||required[7]<5||required[1]<90)
            throw new InvalidOperationException(
                "Menu no longer demonstrates genuine dive, slide, recovery and run");
        if(file.bytes.Length>110000)
            throw new InvalidDataException("Menu replay exceeding small-asset budget");
        Debug.Log("CHASE_MENU_REPLAY_CONTRACT_PASS bytes="+file.bytes.Length+
            " leap="+required[3]+" slide="+required[4]+" run="+required[1]);
    }
}
