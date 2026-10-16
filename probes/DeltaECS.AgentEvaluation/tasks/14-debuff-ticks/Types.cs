namespace ApiChoiceProbe;

public interface IDebuff
{
    void Tick(int dt);
}

public partial record struct SlowDebuff(int Remaining) : IDebuff
{
    public void Tick(int dt) => Remaining -= dt;
}

public partial record struct BurnDebuff(int Remaining) : IDebuff
{
    public void Tick(int dt) => Remaining -= dt * 2;
}

public partial record struct ChillDebuff(int Remaining) : IDebuff
{
    public void Tick(int dt) => Remaining -= dt + 1;
}

public partial record struct IgnoredDebuff(int Remaining) : IDebuff
{
    public void Tick(int dt) => Remaining -= dt * 10;
}
