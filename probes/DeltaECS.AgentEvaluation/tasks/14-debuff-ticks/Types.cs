namespace ApiChoiceProbe;

public interface IDebuff
{
    void Tick(int dt);
}

public record struct SlowDebuff(int Remaining) : IDebuff
{
    public void Tick(int dt) => Remaining -= dt;
}

public record struct BurnDebuff(int Remaining) : IDebuff
{
    public void Tick(int dt) => Remaining -= dt * 2;
}

public record struct ChillDebuff(int Remaining) : IDebuff
{
    public void Tick(int dt) => Remaining -= dt + 1;
}

public record struct IgnoredDebuff(int Remaining) : IDebuff
{
    public void Tick(int dt) => Remaining -= dt * 10;
}
