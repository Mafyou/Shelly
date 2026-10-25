namespace ShellyBLE.ViewModels;

public sealed record Info(string Text);

public sealed record Problem(string Text);

/// <summary>Message affiché en haut d'un écran après une opération.</summary>
public union Feedback(Info, Problem);

public static class FeedbackExtensions
{
    extension(Feedback feedback)
    {
        public string Text => feedback switch
        {
            Info info => info.Text,
            Problem problem => problem.Text,
        };

        public bool IsProblem => feedback is Problem;
    }
}
