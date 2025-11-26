using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;

namespace XCommander.Services;

/// <summary>
/// Manages panel and navigation animations for smooth UX transitions.
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// Duration for quick transitions (panel switches).
    /// </summary>
    public static readonly TimeSpan QuickDuration = TimeSpan.FromMilliseconds(150);
    
    /// <summary>
    /// Duration for standard transitions (navigation).
    /// </summary>
    public static readonly TimeSpan StandardDuration = TimeSpan.FromMilliseconds(200);
    
    /// <summary>
    /// Duration for slow transitions (major state changes).
    /// </summary>
    public static readonly TimeSpan SlowDuration = TimeSpan.FromMilliseconds(300);
    
    /// <summary>
    /// Default easing function for smooth animations.
    /// </summary>
    public static readonly Easing DefaultEasing = new CubicEaseOut();
    
    /// <summary>
    /// Create a fade transition animation.
    /// </summary>
    public static Animation CreateFadeAnimation(double from, double to, TimeSpan duration)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = DefaultEasing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(Visual.OpacityProperty, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(Visual.OpacityProperty, to) }
                }
            }
        };
        return animation;
    }
    
    /// <summary>
    /// Create a slide animation for navigation.
    /// </summary>
    public static Animation CreateSlideAnimation(double fromX, double toX, TimeSpan duration)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = DefaultEasing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(TranslateTransform.XProperty, fromX) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(TranslateTransform.XProperty, toX) }
                }
            }
        };
        return animation;
    }
    
    /// <summary>
    /// Create a scale animation for emphasis.
    /// </summary>
    public static Animation CreateScaleAnimation(double fromScale, double toScale, TimeSpan duration)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = new BounceEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(ScaleTransform.ScaleXProperty, fromScale),
                        new Setter(ScaleTransform.ScaleYProperty, fromScale)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(ScaleTransform.ScaleXProperty, toScale),
                        new Setter(ScaleTransform.ScaleYProperty, toScale)
                    }
                }
            }
        };
        return animation;
    }
    
    /// <summary>
    /// Create a panel switch highlight animation.
    /// </summary>
    public static Animation CreatePanelSwitchAnimation()
    {
        return new Animation
        {
            Duration = QuickDuration,
            Easing = DefaultEasing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 0.7),
                        new Setter(Border.BorderThicknessProperty, new Thickness(2))
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 1.0),
                        new Setter(Border.BorderThicknessProperty, new Thickness(0))
                    }
                }
            }
        };
    }
}

/// <summary>
/// Page transition effects for navigation.
/// </summary>
public class NavigationTransition
{
    public enum TransitionDirection
    {
        Forward,  // Navigate into a folder
        Backward, // Navigate up to parent
        Refresh   // Same level refresh
    }
    
    private readonly Control _target;
    
    public NavigationTransition(Control target)
    {
        _target = target;
    }
    
    /// <summary>
    /// Apply slide transition based on navigation direction.
    /// </summary>
    public async Task AnimateNavigationAsync(TransitionDirection direction)
    {
        var width = _target.Bounds.Width;
        if (width <= 0) width = 400; // Default width if not yet measured
        
        double fromX = direction switch
        {
            TransitionDirection.Forward => width * 0.1,
            TransitionDirection.Backward => -width * 0.1,
            TransitionDirection.Refresh => 0,
            _ => 0
        };
        
        // Set initial state
        _target.RenderTransform = new TranslateTransform(fromX, 0);
        _target.Opacity = 0.5;
        
        // Animate to final state
        var slideAnimation = AnimationHelper.CreateSlideAnimation(fromX, 0, AnimationHelper.StandardDuration);
        var fadeAnimation = AnimationHelper.CreateFadeAnimation(0.5, 1.0, AnimationHelper.StandardDuration);
        
        // Run animations
        await Task.WhenAll(
            slideAnimation.RunAsync(_target),
            fadeAnimation.RunAsync(_target)
        );
        
        // Reset transform
        _target.RenderTransform = null;
    }
    
    /// <summary>
    /// Quick fade for content updates.
    /// </summary>
    public async Task AnimateContentUpdateAsync()
    {
        _target.Opacity = 0.8;
        var animation = AnimationHelper.CreateFadeAnimation(0.8, 1.0, AnimationHelper.QuickDuration);
        await animation.RunAsync(_target);
    }
}

/// <summary>
/// Panel focus and activation animations.
/// </summary>
public class PanelActivationAnimator
{
    private readonly Border _activeIndicator;
    private readonly Control _panel;
    
    public PanelActivationAnimator(Control panel, Border activeIndicator)
    {
        _panel = panel;
        _activeIndicator = activeIndicator;
    }
    
    /// <summary>
    /// Animate panel becoming active.
    /// </summary>
    public async Task AnimateActivation()
    {
        // Flash the active indicator
        _activeIndicator.Opacity = 0;
        _activeIndicator.IsVisible = true;
        
        var animation = AnimationHelper.CreateFadeAnimation(0, 1, AnimationHelper.QuickDuration);
        await animation.RunAsync(_activeIndicator);
    }
    
    /// <summary>
    /// Animate panel becoming inactive.
    /// </summary>
    public async Task AnimateDeactivation()
    {
        var animation = AnimationHelper.CreateFadeAnimation(1, 0.6, AnimationHelper.QuickDuration);
        await animation.RunAsync(_activeIndicator);
    }
}
