using Godot;

namespace Orbital;

public sealed class Sound
{
    private readonly AudioStreamPlayer _music;
    private readonly AudioStreamPlayer[] _voices;
    private readonly Dictionary<string,AudioStreamWav> _effects=new();
    private readonly Bindings _settings;
    private int _next;
    public Sound(Node parent,Bindings settings)
    {
        _settings=settings;
        _music=new AudioStreamPlayer(); parent.AddChild(_music);
        _voices=Enumerable.Range(0,10).Select(_=>new AudioStreamPlayer()).ToArray();
        foreach(var voice in _voices) parent.AddChild(voice);
        _effects["shot"]=Synthesize(.12,880,170,.25);
        _effects["rocket"]=Synthesize(.25,150,48,.5);
        _effects["burst"]=Synthesize(.42,80,30,.85);
        _effects["dash"]=Synthesize(.22,480,110,.65);
        _effects["hit"]=Synthesize(.18,220,50,.7);
        _effects["click"]=Synthesize(.05,1100,600,.05);
        PlayMusic();
    }
    public void PlayMusic()
    {
        _music.Stop();
        var path=_settings.MusicTrack==0?"res://orbital_assault_138bpm.wav":"res://orbital_overdrive_150bpm.wav";
        var wav=GD.Load<AudioStreamWav>(path);
        wav.LoopMode=AudioStreamWav.LoopModeEnum.Forward;
        wav.LoopEnd=wav.Data.Length/(wav.Stereo?4:2);
        _music.Stream=wav; Apply(); _music.Play();
    }
    public void Apply()
    {
        _music.VolumeDb=_settings.MusicVolume<=0?-80:Mathf.LinearToDb(_settings.MusicVolume)-9;
    }
    public void Play(string key,float strength=1)
    {
        if(_settings.EffectsVolume<=0) return;
        var voice=_voices[_next++ % _voices.Length]; voice.Stop(); voice.Stream=_effects[key];
        voice.VolumeDb=Mathf.LinearToDb(Math.Max(.001f,_settings.EffectsVolume*strength))-13;
        voice.Play();
    }
    public void Stop()
    {
        _music.Stop(); _music.Stream=null;
        foreach(var voice in _voices) { voice.Stop();voice.Stream=null; }
    }
    private static AudioStreamWav Synthesize(double length,double start,double end,double noise)
    {
        const int rate=22050; int count=(int)(rate*length);
        var bytes=new byte[count*2]; double phase=0;
        var random=new Random(47);
        for(int i=0;i<count;i++)
        {
            double t=(double)i/count;
            phase+=Math.Tau*(end+(start-end)*Math.Exp(-t*6))/rate;
            double envelope=Math.Min(t*80,1)*Math.Exp(-t*5)*(1-t);
            var sample=(short)((Math.Sin(phase)*(1-noise)+(random.NextDouble()*2-1)*noise)*envelope*22000);
            bytes[i*2]=(byte)sample; bytes[i*2+1]=(byte)(sample>>8);
        }
        return new AudioStreamWav { Format=AudioStreamWav.FormatEnum.Format16Bits, MixRate=rate, Data=bytes };
    }
}
