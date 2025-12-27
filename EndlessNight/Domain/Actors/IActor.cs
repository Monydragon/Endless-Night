using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain.Actors;

public interface IActor : IRunScoped, INamedEntity
{
    bool IsAlive { get; set; }

    Guid CurrentRoomId { get; set; }

    int Intensity { get; set; }

    /// </summary>
    /// 0..100. Used as a difficulty/intensity driver for encounters.
    /// <summary>

    int Morality { get; set; }

    /// </summary>
    /// -100..100 (Evil..Good). 0 is neutral.
    /// <summary>

    ActorKind Kind { get; }
}



