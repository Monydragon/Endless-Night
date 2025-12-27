namespace EndlessNight.Domain.Actors;

public interface INpc : IActor
{
    /// <summary>
    /// 0..100 (higher is better). NPCs can lose sanity in the Endless Night too.
    /// </summary>
    int Sanity { get; set; }
}
                                                                                                                                                                                                                                                                                             
