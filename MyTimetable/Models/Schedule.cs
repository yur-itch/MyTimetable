using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyTimetable.Models
{
    public record class Cell
    {
        public required DefaultLesson? DefaultLesson { get; set; }
        public required CustomLesson? CustomLesson { get; set; }
        public required bool Hidden { get; set; }
    }

    public record class DaySchedule
    {
        public required DateOnly Date;
        public required Cell[] Cells;
    }

    public record class Professor
    {
        public required string Name;
        public required bool IsShown;
    }

    public record class Lesson
    {
        public required string Title { get; set; }
        public required string LessonType { get; set; }
    }

    public record class DefaultLesson : Lesson
    {
        public required Professor Professor { get; set; }
        public required string Room { get; set; }
    }
    public record class CustomLesson : Lesson { }
    
}
