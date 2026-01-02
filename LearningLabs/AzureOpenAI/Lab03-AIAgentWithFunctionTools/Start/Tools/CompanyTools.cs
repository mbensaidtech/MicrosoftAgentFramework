using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AIAgentWithFunctionTools.Tools;

/// <summary>
/// Internal company tools that provide real-time data the LLM cannot know from training.
/// These tools give the agent access to internal systems: employees, meeting rooms, vacations, etc.
/// </summary>
public class CompanyTools
{
    // Simulated internal database
    private static readonly Dictionary<string, Employee> _employees = new()
    {
        ["EMP001"] = new Employee("EMP001", "Mohammed BEN SAID", "Engineering", "Senior Developer", "mohammed.bensaid@company.com", 25),
        ["EMP002"] = new Employee("EMP002", "Bob Johnson", "Marketing", "Marketing Manager", "bob.johnson@company.com", 18),
        ["EMP003"] = new Employee("EMP003", "Charlie Brown", "Engineering", "Tech Lead", "charlie.brown@company.com", 12),
        ["EMP004"] = new Employee("EMP004", "Sara BEN SAID", "HR", "HR Director", "sara.bensaid@company.com", 30),
        ["EMP005"] = new Employee("EMP005", "Edward Smith", "Finance", "Financial Analyst", "edward.smith@company.com", 22)
    };

    private static readonly Dictionary<string, MeetingRoom> _meetingRooms = new()
    {
        ["ROOM-A"] = new MeetingRoom("ROOM-A", "Innovation Lab", 10, new[] { "Projector", "Whiteboard", "Video Conference" }),
        ["ROOM-B"] = new MeetingRoom("ROOM-B", "Brainstorm Corner", 6, new[] { "Whiteboard", "TV Screen" }),
        ["ROOM-C"] = new MeetingRoom("ROOM-C", "Executive Suite", 20, new[] { "Projector", "Video Conference", "Catering Service" }),
        ["ROOM-D"] = new MeetingRoom("ROOM-D", "Quick Sync", 4, new[] { "TV Screen" })
    };

    private static readonly List<RoomBooking> _bookings = new()
    {
        new RoomBooking("ROOM-A", "EMP001", DateTime.Today.AddHours(9), DateTime.Today.AddHours(10), "Sprint Planning"),
        new RoomBooking("ROOM-C", "EMP004", DateTime.Today.AddHours(14), DateTime.Today.AddHours(16), "HR Meeting")
    };

    /// <summary>
    /// Gets employee information by their employee ID.
    /// </summary>
    // TODO: Add [Description] attribute to describe the method to the LLM
    // Hint: The Description attribute helps the LLM understand when to use this function
    public string GetEmployeeInfo(
        // TODO: Add [Description] attribute to describe the parameter to the LLM
        string employeeId)
    {
        if (_employees.TryGetValue(employeeId.ToUpper(), out var employee))
        {
            return $"Employee Found:\n" +
                   $"- ID: {employee.Id}\n" +
                   $"- Name: {employee.Name}\n" +
                   $"- Department: {employee.Department}\n" +
                   $"- Position: {employee.Position}\n" +
                   $"- Email: {employee.Email}\n" +
                   $"- Vacation Days Remaining: {employee.VacationDaysRemaining}";
        }
        return $"Employee with ID '{employeeId}' not found.";
    }

    /// <summary>
    /// Searches for employees by name or department.
    /// </summary>
    [Description("Searches for employees by name (partial match) or department.")]
    private string SearchEmployees(
        [Description("Search term - can be part of a name or a department name")] string searchTerm)
    {
        var results = _employees.Values
            .Where(e => e.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        e.Department.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (results.Count == 0)
            return $"No employees found matching '{searchTerm}'.";

        var output = $"Found {results.Count} employee(s):\n";
        foreach (var emp in results)
        {
            output += $"- {emp.Id}: {emp.Name} ({emp.Department} - {emp.Position})\n";
        }
        return output;
    }

    /// <summary>
    /// Gets the list of available meeting rooms with their features.
    /// </summary>
    [Description("Lists all available meeting rooms with their capacity and features.")]
    public string GetMeetingRooms()
    {
        var output = "Available Meeting Rooms:\n";
        foreach (var room in _meetingRooms.Values)
        {
            output += $"\n {room.Name} ({room.Id})\n";
            output += $"   Capacity: {room.Capacity} people\n";
            output += $"   Features: {string.Join(", ", room.Features)}\n";
        }
        return output;
    }

    /// <summary>
    /// Checks meeting room availability for a specific date and time.
    /// </summary>
    [Description("Checks if a meeting room is available at a specific date and time.")]
    private string CheckRoomAvailability(
        [Description("The room ID (e.g., ROOM-A, ROOM-B)")] string roomId,
        [Description("The date to check (format: yyyy-MM-dd)")] string date,
        [Description("Start time (format: HH:mm, e.g., 09:00)")] string startTime,
        [Description("End time (format: HH:mm, e.g., 10:30)")] string endTime)
    {
        if (!_meetingRooms.ContainsKey(roomId.ToUpper()))
            return $"Room '{roomId}' not found.";

        if (!DateTime.TryParse($"{date} {startTime}", out var start) ||
            !DateTime.TryParse($"{date} {endTime}", out var end))
            return "Invalid date or time format.";

        var conflicts = _bookings
            .Where(b => b.RoomId == roomId.ToUpper() &&
                        b.StartTime < end && b.EndTime > start)
            .ToList();

        if (conflicts.Count == 0)
            return $"Room {roomId} is AVAILABLE from {startTime} to {endTime} on {date}.";

        var output = $"Room {roomId} is NOT available. Conflicts:\n";
        foreach (var booking in conflicts)
        {
            output += $"- {booking.StartTime:HH:mm} to {booking.EndTime:HH:mm}: {booking.Subject} (booked by {booking.BookedBy})\n";
        }
        return output;
    }

    /// <summary>
    /// Books a meeting room for a specific date and time.
    /// </summary>
    [Description("Books a meeting room for a specific date, time, and subject.")]
    public string BookMeetingRoom(
        [Description("The room ID (e.g., ROOM-A)")] string roomId,
        [Description("The employee ID making the booking")] string employeeId,
        [Description("The date (format: yyyy-MM-dd)")] string date,
        [Description("Start time (format: HH:mm)")] string startTime,
        [Description("End time (format: HH:mm)")] string endTime,
        [Description("Meeting subject/title")] string subject)
    {
        roomId = roomId.ToUpper();
        employeeId = employeeId.ToUpper();

        if (!_meetingRooms.ContainsKey(roomId))
            return $"Room '{roomId}' not found.";

        if (!_employees.ContainsKey(employeeId))
            return $"Employee '{employeeId}' not found.";

        if (!DateTime.TryParse($"{date} {startTime}", out var start) ||
            !DateTime.TryParse($"{date} {endTime}", out var end))
            return "Invalid date or time format.";

        var conflicts = _bookings
            .Where(b => b.RoomId == roomId && b.StartTime < end && b.EndTime > start)
            .Any();

        if (conflicts)
            return $" Cannot book: Room {roomId} is already booked during this time.";

        _bookings.Add(new RoomBooking(roomId, employeeId, start, end, subject));
        var room = _meetingRooms[roomId];
        
        return $"Booking confirmed!\n" +
               $"- Room: {room.Name} ({roomId})\n" +
               $"- Date: {date}\n" +
               $"- Time: {startTime} - {endTime}\n" +
               $"- Subject: {subject}\n" +
               $"- Booked by: {_employees[employeeId].Name}";
    }

    /// <summary>
    /// Gets the vacation balance for an employee.
    /// </summary>
    [Description("Gets the remaining vacation days for an employee.")]
    private string GetVacationBalance(
        [Description("The employee ID")] string employeeId)
    {
        if (_employees.TryGetValue(employeeId.ToUpper(), out var employee))
        {
            return $"Vacation balance for {employee.Name}:\n" +
                   $"- Remaining days: {employee.VacationDaysRemaining}\n" +
                   $"- Annual allowance: 30 days\n" +
                   $"- Days used: {30 - employee.VacationDaysRemaining}";
        }
        return $"Employee with ID '{employeeId}' not found.";
    }

    /// <summary>
    /// Gets today's date and current time (real-time information).
    /// </summary>
    [Description("Gets the current date and time.")]
    private string GetCurrentDateTime()
    {
        var now = DateTime.Now;
        return $"Current date and time: {now:dddd, MMMM dd, yyyy 'at' HH:mm:ss}";
    }
}

// Supporting record types
public record Employee(string Id, string Name, string Department, string Position, string Email, int VacationDaysRemaining);
public record MeetingRoom(string Id, string Name, int Capacity, string[] Features);
public record RoomBooking(string RoomId, string BookedBy, DateTime StartTime, DateTime EndTime, string Subject);

