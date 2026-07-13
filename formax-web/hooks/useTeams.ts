import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getAllTeams, getMyTeams, setMyTeams } from "@/lib/api/teams";

export function useAllTeams() {
  return useQuery({
    queryKey: ["teams", "all"],
    queryFn: getAllTeams,
    staleTime: 10 * 60 * 1000,
  });
}

export function useMyTeams() {
  return useQuery({
    queryKey: ["teams", "mine"],
    queryFn: getMyTeams,
    staleTime: 5 * 60 * 1000,
  });
}

export function useSetMyTeams() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: setMyTeams,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teams", "mine"] });
    },
  });
}
