namespace CdgFromYoutube;

/// <summary>
/// Receives the progress of a conversion so that it can be shown to the user.
/// </summary>
public interface IProgressSink
{
    /// <summary>Reports that a new step of the conversion has begun.</summary>
    void Stage(string message);

    /// <summary>Reports a detail of the current step.</summary>
    void Detail(string message);

    /// <summary>Reports something that did not stop the conversion but is worth knowing about.</summary>
    void Warning(string message);

    /// <summary>Reports how far the current step has come, on a line that is rewritten in place.</summary>
    /// <param name="fraction">The completed fraction of the step, from zero to one.</param>
    /// <param name="message">The text to show next to the progress bar.</param>
    void Progress(double fraction, string message);

    /// <summary>Ends the line that <see cref="Progress"/> was writing to.</summary>
    void FinishProgress();

    /// <summary>Reports that the conversion finished.</summary>
    void Completed(string message);
}
