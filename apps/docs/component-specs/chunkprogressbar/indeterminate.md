---
title: Indeterminate State
page_title: Indeterminate state for the ChunkProgressBar
description: Indeterminate state for the ChunkProgressBar for Blazor.
slug: chunkprogressbar-indeterminate-state
tags: sunfish,blazor,chunk,progress,bar,progressbar,indeterminate,state
published: True
position: 10
components: ["chunkprogressbar"]
---
# Indeterminate state

The Indeterminate state of the ChunkProgressBar can be used in cases when the estimated time of completion of the task is unknown or the progress can not be represented as a physical value. The ChunkProgressBar is filled with flowing motion animation to showcase that the task is running. 

>caption Sunfish ChunkProgressBar in Indeterminate state

![chunkprogress bar in indeterminate state](images/chunkprogressbar-indeterminate-state-basic-example.png)


To put the ChunkProgressBar in Indeterminate state set the `Indeterminate`, `boolean` parameter to `true`.

>caption Put the ChunkProgressBar in Indeterminate State

````RAZOR
<div>
    <SunfishButton ThemeColor="primary" OnClick="(_ => isIndeterminate = true)">Put the ChunkProgressBar in Indeterminate State</SunfishButton>
</div>

<br />

<SunfishChunkProgressBar Indeterminate="@isIndeterminate" Value="10" Max="100" ChunkCount="10"/>

@code {
    public bool isIndeterminate { get; set; } = false;
}
````

![put the progress bar in indeterminate state example](images/chunkprogress-bar-in-indeterminate-state-example.gif)


## See Also

  * [Live Demo: ChunkProgressBar Overview](https://demos.sunfish.dev/blazor-ui/chunkprogressbar/overview)
  * [Overview](slug:chunkprogressbar-overview)
  
