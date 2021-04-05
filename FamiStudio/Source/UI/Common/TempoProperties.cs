﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FamiStudio
{
    class TempoProperties
    {
        private PropertyPage props;
        private Song song;

        private int patternIdx    = -1;
        private int minPatternIdx = -1;
        private int maxPatternIdx = -1;

        int firstPropIdx             = -1;
        int famitrackerTempoPropIdx  = -1;
        int famitrackerSpeedPropIdx  = -1;
        int notesPerBeatPropIdx      = -1;
        int notesPerPatternPropIdx   = -1;
        int bpmLabelPropIdx          = -1;
        int famistudioBpmPropIdx     = -1;
        int framesPerNotePropIdx     = -1;
        int groovePropIdx            = -1;
        int groovePadPropIdx         = -1;

        int originalNoteLength;
        int originalNotesPerBeat;
        int originalNotesPerPattern;

        TempoInfo[] tempoList;
        string[]    tempoStrings;
        string[]    grooveStrings;

        public TempoProperties(PropertyPage props, Song song, int patternIdx = -1, int minPatternIdx = -1, int maxPatternIdx = -1)
        {
            this.song = song;
            this.props = props;
            this.patternIdx = patternIdx;
            this.minPatternIdx = minPatternIdx;
            this.maxPatternIdx = maxPatternIdx;
            this.firstPropIdx = props.PropertyCount;

            props.PropertyChanged += Props_PropertyChanged;
        }

        public void AddProperties()
        {
            if (song.UsesFamiTrackerTempo)
            {
                if (patternIdx < 0)
                {
                    famitrackerTempoPropIdx = props.AddIntegerRange("Tempo :", song.FamitrackerTempo, 32, 255, CommonTooltips.Tempo); // 0
                    famitrackerSpeedPropIdx = props.AddIntegerRange("Speed :", song.FamitrackerSpeed, 1, 31, CommonTooltips.Speed); // 1
                }
                
                var notesPerBeat = patternIdx < 0 ? song.BeatLength : song.GetPatternBeatLength(patternIdx);
                var notesPerPattern = patternIdx < 0 ? song.PatternLength : song.GetPatternLength(patternIdx);
                var bpm = Song.ComputeFamiTrackerBPM(song.Project.PalMode, song.FamitrackerSpeed, song.FamitrackerTempo, notesPerBeat);

                notesPerBeatPropIdx    = props.AddIntegerRange("Notes per Beat :", notesPerBeat, 1, 256, CommonTooltips.NotesPerBar); // 2
                notesPerPatternPropIdx = props.AddIntegerRange("Notes per Pattern :", notesPerPattern, 1, 256, CommonTooltips.NotesPerPattern); // 3
                bpmLabelPropIdx        = props.AddLabel("BPM :", bpm.ToString("n1"), CommonTooltips.BPM); // 4

                //  // TEMPOTODO : Add a warning to recommend 4 notes per beats too here.
            }
            else
            {                                                              
                var noteLength      = (patternIdx < 0 ? song.NoteLength    : song.GetPatternNoteLength(patternIdx));
                var notesPerBeat    = (patternIdx < 0 ? song.BeatLength    : song.GetPatternBeatLength(patternIdx));
                var notesPerPattern = (patternIdx < 0 ? song.PatternLength : song.GetPatternLength(patternIdx));
                var groove          = (patternIdx < 0 ? song.Groove        : song.GetPatternGroove(patternIdx));

                tempoList = FamiStudioTempoUtils.GetAvailableTempos(song.Project.PalMode, notesPerBeat / noteLength);
                var tempoIndex = Array.FindIndex(tempoList, t => Utils.CompareFloats(t.bpm, song.BPM));
                Debug.Assert(tempoIndex >= 0);
                tempoStrings = tempoList.Select(t => t.bpm.ToString("n1") + (t.groove.Length == 1 ? " *": "")).ToArray();

                var grooveList = FamiStudioTempoUtils.GetAvailableGrooves(tempoList[tempoIndex].groove);
                var grooveIndex = Array.FindIndex(grooveList, g => Utils.CompareArrays(g, groove) == 0);
                Debug.Assert(grooveIndex >= 0);
                grooveStrings = grooveList.Select(g => string.Join("-", g)).ToArray();

                famistudioBpmPropIdx   = props.AddDropDownList("BPM : ", tempoStrings, tempoStrings[tempoIndex]); // 0
                notesPerBeatPropIdx    = props.AddIntegerRange("Notes per Beat : ", notesPerBeat / noteLength, 1, 256, CommonTooltips.NotesPerBar); // 1
                notesPerPatternPropIdx = props.AddIntegerRange("Notes per Pattern : ", notesPerPattern / noteLength, 1, Pattern.MaxLength / noteLength, CommonTooltips.NotesPerPattern); // 2
                framesPerNotePropIdx   = props.AddLabel("Frames per Note :", noteLength.ToString()); // 3

                props.ShowWarnings = true;
                props.BeginAdvancedProperties();
                groovePropIdx    = props.AddDropDownList("Groove : ", grooveStrings, grooveStrings[0] /*  TEMPOTODO : grooveStrings[grooveIndex]*/); // 4
                groovePadPropIdx = props.AddDropDownList("Groove Padding : ", GroovePaddingType.Names, GroovePaddingType.Names[song.GroovePaddingMode]); // 5

                originalNoteLength      = noteLength;
                originalNotesPerBeat    = notesPerBeat;
                originalNotesPerPattern = notesPerPattern;

                UpdateWarnings();
            }
        }

        private void Props_PropertyChanged(PropertyPage props, int idx, object value)
        {
            if (song.UsesFamiTrackerTempo)
            {
                var tempo = song.FamitrackerTempo;
                var speed = song.FamitrackerSpeed;

                if (idx == famitrackerTempoPropIdx ||
                    idx == famitrackerSpeedPropIdx)
                {
                    tempo = props.GetPropertyValue<int>(famitrackerTempoPropIdx);
                    speed = props.GetPropertyValue<int>(famitrackerSpeedPropIdx);
                }

                var beatLength = props.GetPropertyValue<int>(notesPerBeatPropIdx);

                props.SetLabelText(bpmLabelPropIdx, Song.ComputeFamiTrackerBPM(song.Project.PalMode, speed, tempo, beatLength).ToString("n1"));
            }
            else
            {
                var notesPerBeat = props.GetPropertyValue<int>(notesPerBeatPropIdx);

                // Changing the number of notes in a beat will affect the list of available BPMs.
                if (idx == notesPerBeatPropIdx)
                {
                    tempoList = FamiStudioTempoUtils.GetAvailableTempos(song.Project.PalMode, notesPerBeat);
                    tempoStrings = tempoList.Select(t => t.bpm.ToString("n1") + (t.groove.Length == 1 ? " *" : "")).ToArray();
                    props.UpdateDropDownListItems(famistudioBpmPropIdx, tempoStrings);
                }

                // Changing the BPM affects the grooves and note length.
                if (idx == famistudioBpmPropIdx ||
                    idx == notesPerBeatPropIdx)
                {
                    var tempoIndex    = Array.IndexOf(tempoStrings, props.GetPropertyValue<string>(famistudioBpmPropIdx));
                    var tempoInfo     = tempoList[tempoIndex];
                    var framesPerNote = Utils.Min(tempoInfo.groove);

                    props.UpdateIntegerRange(notesPerPatternPropIdx, 1, Pattern.MaxLength / framesPerNote);

                    var grooveList = FamiStudioTempoUtils.GetAvailableGrooves(tempoInfo.groove);
                    grooveStrings = grooveList.Select(g => string.Join("-", g)).ToArray();

                    props.UpdateDropDownListItems(groovePropIdx, grooveStrings);
                    props.SetLabelText(framesPerNotePropIdx, framesPerNote.ToString());
                }

                UpdateWarnings();
            }

            // Custom settings:

            //else if (Song.UsesFamiStudioTempo && (idx == 1 || idx == 2))
            //{
            //    var noteLength = props.GetPropertyValue<int>(1);
            //    var beatLength = props.GetPropertyValue<int>(2);

            //    props.UpdateIntegerRange(2, 1, Pattern.MaxLength / noteLength);
            //    props.SetLabelText(4, Song.ComputeFamiStudioBPM(song.Project.PalMode, noteLength, beatLength * noteLength).ToString("n1"));
            //}
            //else if (Song.UsesFamiTrackerTempo && idx == 1)
            //{
            //    var beatLength = (int)value;

            //    props.SetLabelText(3, Song.ComputeFamiTrackerBPM(song.Project.PalMode, song.FamitrackerSpeed, song.FamitrackerTempo, beatLength).ToString("n1"));
            //}
        }

        private void UpdateWarnings()
        {
            Debug.Assert(song.UsesFamiStudioTempo);

            var tempoIndex = Array.IndexOf(tempoStrings, props.GetPropertyValue<string>(famistudioBpmPropIdx));
            var tempoInfo = tempoList[tempoIndex];
            var notesPerBeat = props.GetPropertyValue<int>(notesPerBeatPropIdx);
            var notesPerPattern = props.GetPropertyValue<int>(notesPerPatternPropIdx);

            if (tempoInfo.groove.Length == 1)
            {
                props.SetPropertyWarning(famistudioBpmPropIdx, CommentType.Good, "Ideal tempo : notes will be perfectly evenly divided.");
            }
            else if ((tempoInfo.groove.Length % notesPerBeat) == 0 ||
                     (notesPerBeat % tempoInfo.groove.Length) == 0)
            {
                props.SetPropertyWarning(famistudioBpmPropIdx, CommentType.Warning, "Beat-aligned groove : notes will be slightly uneven, but well aligned with the beat.");
            }
            else
            {
                props.SetPropertyWarning(famistudioBpmPropIdx, CommentType.Error, "Unaligned groove : notes will be slightly uneven and not aligned to the beat.");
            }

            if (notesPerBeat != 4)
            {
                props.SetPropertyWarning(notesPerBeatPropIdx, CommentType.Error, "A value of 4 is strongly recommended as it gives the best range of available BPMs.");
            }
            else
            {
                props.SetPropertyWarning(notesPerBeatPropIdx, CommentType.Good, "4 is the recommended value.");
            }

            if (originalNotesPerPattern != notesPerPattern)
            {
                // TEMPOTODO : This is super wrong.
                props.SetPropertyWarning(notesPerPatternPropIdx, CommentType.Warning, $"{Math.Abs(originalNotesPerPattern - notesPerPattern)} notes will be {(originalNotesPerPattern > notesPerPattern ? "truncated" : "added")} in every pattern.");
            }
            else
            {
                props.SetPropertyWarning(notesPerPatternPropIdx, CommentType.Warning, "");
            }
        }

        public void EnableProperties(bool enabled)
        {
            for (var i = firstPropIdx; i < props.PropertyCount; i++)
                props.SetPropertyEnabled(i, enabled);
        }

        public void Apply(bool custom = false)
        {
            if (song.UsesFamiTrackerTempo)
            {
                if (patternIdx == -1)
                {
                    if (famitrackerTempoPropIdx >= 0)
                    {
                        song.FamitrackerTempo = props.GetPropertyValue<int>(famitrackerTempoPropIdx);
                        song.FamitrackerSpeed = props.GetPropertyValue<int>(famitrackerSpeedPropIdx);
                    }

                    song.SetBeatLength(props.GetPropertyValue<int>(notesPerBeatPropIdx));
                    song.SetDefaultPatternLength(props.GetPropertyValue<int>(notesPerPatternPropIdx));
                }
                else
                {
                    for (int i = minPatternIdx; i <= maxPatternIdx; i++)
                    {
                        var beatLength    = props.GetPropertyValue<int>(notesPerBeatPropIdx);
                        var patternLength = props.GetPropertyValue<int>(notesPerPatternPropIdx);

                        if (custom)
                            song.SetPatternCustomSettings(i, patternLength, beatLength);
                        else
                            song.ClearPatternCustomSettings(i);
                    }
                }
            }
            else
            {
                var tempoIndex      = Array.IndexOf(tempoStrings, props.GetPropertyValue<string>(famistudioBpmPropIdx));
                var tempoInfo       = tempoList[tempoIndex];

                var notesPerBeat    = props.GetPropertyValue<int>(notesPerBeatPropIdx);
                var notesPerPattern = props.GetPropertyValue<int>(notesPerPatternPropIdx);
                var framesPerNote   = Utils.Min(tempoInfo.groove);

                var grooveIndex     = Array.IndexOf(grooveStrings, props.GetPropertyValue<string>(groovePropIdx));
                var groovePadding   = GroovePaddingType.GetValueForName(props.GetPropertyValue<string>(groovePadPropIdx));
                var grooveList      = FamiStudioTempoUtils.GetAvailableGrooves(tempoInfo.groove);

                props.UpdateIntegerRange(notesPerPatternPropIdx, 1, Pattern.MaxLength / framesPerNote);
                props.SetLabelText(framesPerNotePropIdx, framesPerNote.ToString());

                if (patternIdx == -1)
                {
                    var convertTempo = false;

                    if (framesPerNote != originalNoteLength) // MATTT : Standardize these terms!
                    {
                        // TEMPOTODO : Better message + conversion here!
                        convertTempo = PlatformUtils.MessageBox($"You changed the note length, do you want FamiStudio to attempt convert the tempo by resizing notes?", "Tempo Change", MessageBoxButtons.YesNo) == DialogResult.Yes;
                    }

                    song.ChangeFamiStudioTempoGroove(tempoInfo.groove, convertTempo); // TEMPOTODO : Use the selected groove from the list!
                    song.SetBeatLength(notesPerBeat * song.NoteLength);
                    song.SetDefaultPatternLength(notesPerPattern * song.NoteLength);
                    song.SetGroovePaddingMode(groovePadding);
                }
                else
                {
                    /*
                    var askedToConvertTempo = false;
                    var convertTempo = false;

                    for (int i = minPatternIdx; i <= maxPatternIdx; i++)
                    {
                        var noteLength    = song.NoteLength;
                        var patternLength = song.PatternLength;
                        var beatLength    = song.BeatLength;

                        if (custom)
                        {
                            noteLength    = props.GetPropertyValue<int>(firstPropertyIndex + 0);
                            beatLength    = props.GetPropertyValue<int>(firstPropertyIndex + 1) * noteLength;
                            patternLength = props.GetPropertyValue<int>(firstPropertyIndex + 2) * noteLength;
                        }

                        if (noteLength != song.GetPatternNoteLength(patternIdx))
                        {
                            if (!askedToConvertTempo)
                            {
                                convertTempo = PlatformUtils.MessageBox($"You changed the note length for this pattern, do you want FamiStudio to attempt convert the tempo by resizing notes?", "Tempo Change", MessageBoxButtons.YesNo) == DialogResult.Yes;
                                askedToConvertTempo = true;
                            }

                            if (convertTempo)
                                song.ResizePatternNotes(i, noteLength);
                        }

                        if (custom)
                            song.SetPatternCustomSettings(i, patternLength, beatLength, noteLength);
                        else
                            song.ClearPatternCustomSettings(i);
                    }
                    */
                }
            }

            song.Project.Validate();
        }
    }
}
