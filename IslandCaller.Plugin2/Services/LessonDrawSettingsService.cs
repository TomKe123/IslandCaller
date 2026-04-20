using ClassIsland.Core.Abstractions.Services;
using IslandCaller.Models;

namespace IslandCaller.Services;

public sealed class LessonDrawSettingsService
{
    private readonly ILessonsService _lessonsService;
    private LessonOverrideState? _currentLessonOverride;

    public LessonDrawSettingsService(ILessonsService lessonsService)
    {
        _lessonsService = lessonsService;
        _lessonsService.CurrentTimeStateChanged += (_, _) => ClearLessonOverride();
    }

    public DrawSelectionScope EffectiveScope => _currentLessonOverride?.Scope switch
    {
        LessonDrawScopeOption.Male => DrawSelectionScope.Male,
        LessonDrawScopeOption.Female => DrawSelectionScope.Female,
        _ => Settings.Instance.General.DefaultDrawScope
    };

    public DrawSelectionAlgorithm EffectiveAlgorithm => _currentLessonOverride?.Algorithm switch
    {
        LessonDrawAlgorithmOption.PureRandom => DrawSelectionAlgorithm.PureRandom,
        _ => Settings.Instance.General.DefaultDrawAlgorithm
    };

    public bool HasLessonOverride => _currentLessonOverride is not null;

    public void ApplyLessonOverride(LessonDrawScopeOption scope, LessonDrawAlgorithmOption algorithm)
    {
        if (scope == LessonDrawScopeOption.FollowMain && algorithm == LessonDrawAlgorithmOption.FollowMain)
        {
            ClearLessonOverride();
            return;
        }

        _currentLessonOverride = new LessonOverrideState(scope, algorithm);
    }

    public void ClearLessonOverride()
    {
        _currentLessonOverride = null;
    }

    public LessonDrawScopeOption GetLessonScopeOption()
    {
        return _currentLessonOverride?.Scope ?? LessonDrawScopeOption.FollowMain;
    }

    public LessonDrawAlgorithmOption GetLessonAlgorithmOption()
    {
        return _currentLessonOverride?.Algorithm ?? LessonDrawAlgorithmOption.FollowMain;
    }

    private readonly record struct LessonOverrideState(
        LessonDrawScopeOption Scope,
        LessonDrawAlgorithmOption Algorithm);
}
