using System.Runtime.InteropServices;

namespace KuzushiBeacon;

// Local PCM playback only. Volume is encoded in the samples, never in the game's/global mixer.
internal sealed class LocalSound : IDisposable
{
    [DllImport("winmm.dll",EntryPoint="PlaySoundW",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool PlaySound(nint sound,nint module,uint flags);
    private GCHandle buffer;
    private double next;
    public void Play(double now,float volume,int tone)
    {
        if(now<next || volume<=0)return;
        next=now+1;
        Stop();
        const int rate=22050,count=4410;
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write("RIFF"u8);writer.Write(36+count*2);writer.Write("WAVEfmt "u8);writer.Write(16);
        writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
        writer.Write("data"u8);writer.Write(count*2);
        var frequency=tone switch{1=>660,2=>880,_=>440};
        for(int i=0;i<count;i++)
        {var envelope=Math.Min(1,i/220d)*Math.Min(1,(count-i)/660d);writer.Write((short)(Math.Sin(i*2*Math.PI*frequency/rate)*12000*Math.Clamp(volume,0,1)*envelope));}
        buffer=GCHandle.Alloc(stream.ToArray(),GCHandleType.Pinned);
        if(!PlaySound(buffer.AddrOfPinnedObject(),0,0x0001|0x0004|0x0002))Stop();
    }
    public void Stop(){if(!buffer.IsAllocated)return;PlaySound(0,0,0);buffer.Free();}
    public void Dispose()=>Stop();
}
