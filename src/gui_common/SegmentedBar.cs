using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
///   A ProgressBar that is split up into IconProgressBars, data is stored in a dictionary
/// </summary>
public class SegmentedBar : Control
{
    public PackedScene IconProgressBarScene = GD.Load<PackedScene>("res://src/gui_common/IconProgressBar.tscn");
    public string Type;
    public float maxValue;

    public float[] Size
    {
        get { return new float[]{ RectSize.x, RectSize.y }; }
        set
        {
            RectSize = new Vector2(value[0], value[1]);
            RectMinSize = RectSize;
        }
    }

    public void updateAndMoveBars(Dictionary<string, float> data)
    {
        removeUnusedBars(this, data);

        data = SortBarData(data);
        int location = 0;
        foreach (var dataPair in data)
        {
            createAndUpdateBar(dataPair, this, location);
            location++;
        }

        foreach (var dataPair in data)
        {
            if (HasNode(dataPair.Key))
                updateDisabledBars(dataPair, this);
        }

        foreach (var dataPair in data)
        {
            if (HasNode(dataPair.Key))
                moveBars(GetNode<IconProgressBar>(dataPair.Key));
        }
    }

    private void removeUnusedBars(SegmentedBar parent, Dictionary<string, float> data)
    {
        List<IconProgressBar> unusedBars = new List<IconProgressBar>();
        foreach (IconProgressBar progressBar in parent.GetChildren())
        {
            bool match = false;
            foreach (var dataPair in data)
            {
                if (progressBar.Name == dataPair.Key)
                    match = true;
            }

            if (!match)
                unusedBars.Add(progressBar);
        }
        foreach(IconProgressBar unusedBar in unusedBars)
            unusedBar.Free();
    }

    private void createAndUpdateBar(KeyValuePair <string, float> dataPair, SegmentedBar parent, int location = -1)
    {
        if (parent.HasNode(dataPair.Key))
        {
            IconProgressBar progressBar = (IconProgressBar)parent.GetNode(dataPair.Key);
            IconBarConfig config = new IconBarConfig(progressBar);
            if (config.Disabled) return;
            config.LeftShift = getPreviousBar(parent, progressBar).RectSize.x + getPreviousBar(parent, progressBar).MarginLeft;
            config.Size = new Vector2((float)Math.Floor(dataPair.Value / parent.maxValue * Size[0]), Size[1]);
        }
        else
        {
            IconProgressBar progressBar = (IconProgressBar)new SegmentedBar().IconProgressBarScene.Instance();
            IconBarConfig config = new IconBarConfig(progressBar);
            parent.AddChild(progressBar);
            config.Name = dataPair.Key;
            config.Colour = BarHelper.GetBarColour(Type, dataPair.Key, GetIndex() == 0);
            config.LeftShift = getPreviousBar(parent, progressBar).RectSize.x + getPreviousBar(parent, progressBar).MarginLeft;
            config.Size = new Vector2((float)Math.Floor(dataPair.Value / parent.maxValue * Size[0]), Size[1]);
            config.Texture = BarHelper.GetBarIcon(Type, dataPair.Key);
            progressBar.Connect("gui_input", this, nameof(BarToggled), new Godot.Collections.Array() { progressBar } );
            if (location >= 0)
            {
                config.Location = location;
                config.ActualLocation = location;
            }
        }
    }

    public void BarToggled(InputEvent @event, IconProgressBar bar)
    {
        if (@event is InputEventMouseButton eventMouse && @event.IsPressed())
        {
            IconBarConfig config = new IconBarConfig(bar);
            config.Disabled = !config.Disabled;
            handleBarDisabling(bar);
        }
    }


    private IconProgressBar getPreviousBar(SegmentedBar parent, IconProgressBar currentBar)
    {
        return currentBar.GetIndex() != 0 ?
            parent.GetChild<IconProgressBar>(currentBar.GetIndex() - 1) : new IconProgressBar();
    }

    private void updateDisabledBars(KeyValuePair <string, float> dataPair, SegmentedBar parent)
    {
        IconProgressBar progressBar = (IconProgressBar)parent.GetNode(dataPair.Key);
        IconBarConfig config = new IconBarConfig(progressBar);
        if (!config.Disabled) return;
        config.LeftShift = getPreviousBar(parent, progressBar).RectSize.x + getPreviousBar(parent, progressBar).MarginLeft;
        config.Size = new Vector2((float)Math.Floor(dataPair.Value / parent.maxValue * Size[0]), Size[1]);
    }

    private void calculateActualLocation(SegmentedBar parentBar)
    {
        List<IconProgressBar> children = new List<IconProgressBar>();
        foreach (IconProgressBar childBar in parentBar.GetChildren())
        {
            children.Add(childBar);
        }

        children = children.OrderBy(bar => {
            IconBarConfig config = new IconBarConfig(bar);
            return config.Location + (config.Disabled ? children.Count : 0);
        }).ToList();

        foreach (var childBar in children)
        {
            IconBarConfig config = new IconBarConfig(childBar);
            config.ActualLocation = children.IndexOf(childBar);
        }
    }

    private void moveByIndexBars(IconProgressBar bar)
    {
        IconBarConfig config = new IconBarConfig(bar);
        bar.GetParent().MoveChild(bar, config.ActualLocation);
    }

    public void handleBarDisabling(IconProgressBar bar)
    {
        IconBarConfig config = new IconBarConfig(bar);
        if (bar.Disabled)
        {
            config.Modulate = new Color(0, 0, 0);
            config.Colour = new Color(0.73f, 0.73f, 0.73f);
            moveBars(bar);
        }
        else
        {
            config.Modulate = new Color(1, 1, 1);
            config.Colour = BarHelper.GetBarColour(Type, bar.Name, GetIndex() == 0);
            moveBars(bar);
        }
    }
    
    private void moveBars(IconProgressBar bar)
    {
        calculateActualLocation(bar.GetParent<SegmentedBar>());
        foreach (IconProgressBar iconBar in bar.GetParent().GetChildren())
            moveByIndexBars(iconBar);

        foreach (IconProgressBar iconBar in bar.GetParent().GetChildren())
        {
            float value = iconBar.RectSize.x / Size[0] * (float)((SegmentedBar)bar.GetParent()).maxValue;
            createAndUpdateBar(new KeyValuePair<string, float>(iconBar.Name, value), (SegmentedBar)bar.GetParent());
        }

        foreach (IconProgressBar iconBar in bar.GetParent().GetChildren())
        {
            float value = iconBar.RectSize.x / Size[0] * (float)((SegmentedBar)bar.GetParent()).maxValue;
            updateDisabledBars(new KeyValuePair<string, float>(iconBar.Name, value), (SegmentedBar)bar.GetParent());
        }
    }
    private Dictionary<string, float> SortBarData (Dictionary<string, float> bar)
    {
        Dictionary<string, float> result = bar;

        result = result.OrderBy(
            i => i.Key)
            .ToList()
            .ToDictionary(x => x.Key, x => x.Value);

        return result;
    }
}
