def format_duration(seconds: float) -> str:
    """Format duration in human-readable form"""
    if seconds < 60:
        return f"{seconds:.0f}s"
    elif seconds < 3600:
        mins = int(seconds // 60)
        secs = int(seconds % 60)
        return f"{mins}m {secs}s"
    else:
        hours = int(seconds // 3600)
        mins = int((seconds % 3600) // 60)
        return f"{hours}h {mins}m"


def prioritized_cores(max_cores: int) -> list[list[int]]:
    """
    Generate core configurations prioritized for i7-14700K topology.

    i7-14700K topology:
    - P-cores (8, with SMT): physical threads 0,2,4,6,8,10,12,14; SMT threads 1,3,5,7,9,11,13,15
    - E-cores (12, no SMT): threads 16-27

    Priority order: P-core physical -> E-cores -> P-core SMT

    Returns list of core configs: [[0], [0,2], [0,2,4], ...]
    """
    p_physical = [0, 2, 4, 6, 8, 10, 12, 14]
    e_cores = list(range(16, 28))
    p_smt = [1, 3, 5, 7, 9, 11, 13, 15]
    priority_order = p_physical + e_cores + p_smt

    result = []
    for n in range(1, min(max_cores, len(priority_order)) + 1):
        result.append(priority_order[:n])
    return result
